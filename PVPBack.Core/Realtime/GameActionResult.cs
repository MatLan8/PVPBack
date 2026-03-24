namespace PVPBack.Core.Realtime;

public class GameActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public object? PublicState { get; set; }
    public GameUiMessage? UiMessage { get; set; }
}