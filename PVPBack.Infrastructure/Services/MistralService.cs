using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace PVPBack.Infrastructure.Services;

public class MistralService : IMistralService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public MistralService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        // Modelį pasiimame iš konfigūracijos, kad ateityje galėtume lengvai pakeisti į Small 5 ar Large
        _model = configuration["Mistral:Model"] ?? "mistral-small-latest";
    }

    public async Task<string> GetAiResponseAsync(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.7 // Galite pridėti papildomų parametrų
        };

        var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", requestBody);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Mistral API Error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MistralResponse>();
        return result?.Choices.FirstOrDefault()?.Message.Content ?? "No response from AI.";
    }
}