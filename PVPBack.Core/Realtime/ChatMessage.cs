namespace PVPBack.Core.Realtime;

public class ChatMessage
{
    public string Nickname { get; set; } = null!;
    public string Message { get; set; } = null!;
    public DateTime SentAtUtc { get; set; }
}