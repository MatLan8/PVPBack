namespace PVPBack.Core.Realtime;


public class GameUiMessage
{
    public string Variant { get; set; } = null!; // success, info, warning, error
    public string Message { get; set; } = null!;
}