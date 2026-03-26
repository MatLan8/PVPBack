using System.Text.Json;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;

namespace PVPBack.Infrastructure.Services;

public class AiEvaluationService : IAiEvaluationService
{
    private readonly IMistralService _mistralService;
    private readonly ISessionEvaluationPromptBuilder _promptBuilder;

    public AiEvaluationService(
        IMistralService mistralService,
        ISessionEvaluationPromptBuilder promptBuilder)
    {
        _mistralService = mistralService;
        _promptBuilder = promptBuilder;
    }

    public async Task<(string Summary, string RawJson)> EvaluateAsync(
        GameSessionRuntime session,
        CancellationToken cancellationToken = default)
    {
        var prompt = _promptBuilder.BuildPrompt(session);
        var aiResponse = await _mistralService.GetAiResponseAsync(prompt, cancellationToken);

        var normalizedJson = NormalizeJsonResponse(aiResponse);
        ValidateEvaluationJson(normalizedJson);

        var summary = ExtractSummary(normalizedJson);

        return (summary, normalizedJson);
    }

    private static string NormalizeJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            throw new InvalidOperationException("AI returned an empty response.");

        var normalized = response.Trim();

        // Remove markdown code fences if the model returns them anyway.
        if (normalized.StartsWith("```"))
        {
            normalized = RemoveCodeFences(normalized).Trim();
        }

        return normalized;
    }

    private static string RemoveCodeFences(string text)
    {
        var trimmed = text.Trim();

        if (!trimmed.StartsWith("```"))
            return trimmed;

        var lines = trimmed.Split('\n').ToList();

        if (lines.Count == 0)
            return trimmed;

        // Remove first fence line
        lines.RemoveAt(0);

        // Remove last fence line if present
        if (lines.Count > 0 && lines[^1].Trim().StartsWith("```"))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines);
    }

    private static void ValidateEvaluationJson(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("AI evaluation response is not a JSON object.");

            if (!root.TryGetProperty("session", out var sessionElement) ||
                sessionElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("AI evaluation JSON is missing 'session'.");
            }

            if (!root.TryGetProperty("teamEvaluation", out var teamEvaluationElement) ||
                teamEvaluationElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("AI evaluation JSON is missing 'teamEvaluation'.");
            }

            if (!root.TryGetProperty("playerEvaluations", out var playerEvaluationsElement) ||
                playerEvaluationsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("AI evaluation JSON is missing 'playerEvaluations'.");
            }

            // Optional deeper checks for important fields
            if (!teamEvaluationElement.TryGetProperty("summary", out _))
            {
                throw new InvalidOperationException("AI evaluation JSON is missing 'teamEvaluation.summary'.");
            }

            if (!sessionElement.TryGetProperty("sessionId", out _))
            {
                throw new InvalidOperationException("AI evaluation JSON is missing 'session.sessionId'.");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("AI evaluation response is not valid JSON.", ex);
        }
    }

    private static string ExtractSummary(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("teamEvaluation", out var teamEvaluationElement) &&
                teamEvaluationElement.ValueKind == JsonValueKind.Object &&
                teamEvaluationElement.TryGetProperty("summary", out var teamSummaryElement))
            {
                return teamSummaryElement.GetString() ?? "AI evaluation completed.";
            }

            if (root.TryGetProperty("session", out var sessionElement) &&
                sessionElement.ValueKind == JsonValueKind.Object &&
                sessionElement.TryGetProperty("summary", out var sessionSummaryElement))
            {
                return sessionSummaryElement.GetString() ?? "AI evaluation completed.";
            }

            return "AI evaluation completed.";
        }
        catch
        {
            return "AI evaluation completed.";
        }
    }
}