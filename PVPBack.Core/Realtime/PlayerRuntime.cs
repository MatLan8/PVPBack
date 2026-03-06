namespace PVPBack.Core.Realtime;

public class PlayerRuntime
{
    public string ConnectionId { get;  }
    public string Nickname { get;  }
    public object? PrivateData { get; set; }

    public PlayerRuntime(string connectionId, string nickname)
    {
        ConnectionId = connectionId;
        Nickname = nickname;
    }
}