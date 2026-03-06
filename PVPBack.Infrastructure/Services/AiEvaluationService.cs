using System.Text.Json;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;

namespace PVPBack.Infrastructure.Services;

public class AiEvaluationService : IAiEvaluationService
{
    public Task<(string Summary, string RawJson)> EvaluateAsync(GameSessionRuntime session)
    {
        var summary =
            $"Session {session.SessionCode} finished with {session.Players.Count} players and {session.ChatLog.Count} chat messages.";

        var rawJson = JsonSerializer.Serialize(new
        {
            sessionCode = session.SessionCode,
            playerCount = session.Players.Count,
            chatCount = session.ChatLog.Count,
            finishedAtUtc = DateTime.UtcNow
        });

        return Task.FromResult((summary, rawJson));
    }
}