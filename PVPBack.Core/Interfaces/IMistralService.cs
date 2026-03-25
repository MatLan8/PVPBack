public interface IMistralService
{
    Task<string> GetAiResponseAsync(string prompt);
}