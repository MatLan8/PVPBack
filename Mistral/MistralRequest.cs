// MistralRequest.cs
public class MistralRequest
{
    public string Model { get; set; } = "mistral-small-latest"; // Mistral Small 4 identifikatorius
    public List<MistralMessage> Messages { get; set; } = new();
}

public class MistralMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

// MistralResponse.cs
public class MistralResponse
{
    public List<MistralChoice> Choices { get; set; } = new();
}

public class MistralChoice
{
    public MistralMessage Message { get; set; } = new();
}