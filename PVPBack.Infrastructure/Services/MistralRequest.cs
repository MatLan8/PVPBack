namespace PVPBack.Infrastructure.Services;

public class MistralResponse
{
    public List<MistralChoice> Choices { get; set; } = new();
}

public class MistralChoice
{
    public MistralMessage Message { get; set; } = new();
}

public class MistralMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}