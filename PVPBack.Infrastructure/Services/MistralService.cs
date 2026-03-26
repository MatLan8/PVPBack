using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PVPBack.Core.Interfaces;

namespace PVPBack.Infrastructure.Services;

public class MistralService : IMistralService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public MistralService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _model = configuration["Mistral:Model"] ?? "mistral-small-latest";
    }

    public async Task<string> GetAiResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.3
        };

        var response = await _httpClient.PostAsJsonAsync(
            "v1/chat/completions",
            requestBody,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mistral API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MistralResponse>(cancellationToken);

        return result?.Choices.FirstOrDefault()?.Message.Content
               ?? throw new InvalidOperationException("No response from AI.");
    }
}