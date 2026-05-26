namespace PVPBack.Core.Realtime;

public class ChatMessage
{
    public string PlayerId { get; set; } = null!;
    public string Nickname { get; set; } = null!;
    public string Message { get; set; } = null!;
    public DateTime SentAtUtc { get; set; }

    /// <summary>
    /// The name of the game round that was active when this message was sent,
    /// e.g. "WordleGame", "ConnectionsGame".
    /// Allows the AI evaluator to immediately know which game context the message belongs to.
    /// </summary>
    public string GameType { get; set; } = "";
}