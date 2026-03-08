namespace PVPBack.Core.Realtime;

public class PlayerRuntime
{
    public string PlayerId { get; }
    public string Nickname { get; private set; }
    public string? ConnectionId { get; private set; }
    public bool IsConnected { get; private set; }
    public object? PrivateData { get; set; }

    public PlayerRuntime(string playerId, string connectionId, string nickname)
    {
        PlayerId = playerId;
        ConnectionId = connectionId;
        Nickname = nickname;
        IsConnected = true;
    }

    public void Reconnect(string connectionId, string nickname)
    {
        ConnectionId = connectionId;
        Nickname = nickname;
        IsConnected = true;
    }

    public void MarkDisconnected()
    {
        IsConnected = false;
        ConnectionId = null;
    }
}