using PVPBack.Core.Realtime.MiniGames;

namespace PVPBack.Core.Realtime;

public class GameSessionRuntime
{
    private readonly object _lock = new();

    public string SessionCode { get; }
    public Guid DbSessionId { get; }
    public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;

    public List<PlayerRuntime> Players { get; } = new();
    public List<ChatMessage> ChatLog { get; } = new();
    public IMiniGame CurrentGame { get; }

    public GameSessionRuntime(string sessionCode, Guid dbSessionId)
    {
        SessionCode = sessionCode;
        DbSessionId = dbSessionId;
        CurrentGame = new ConnectionsGame();
    }

    public PlayerRuntime AddPlayer(string connectionId, string nickname)
    {
        lock (_lock)
        {
            var existing = Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (existing is not null)
                return existing;

            var player = new PlayerRuntime(connectionId, nickname);
            Players.Add(player);

            if (Players.Count == 4)
            {
                CurrentGame.Start(Players);
            }

            return player;
        }
    }

    public bool RemovePlayer(string connectionId)
    {
        lock (_lock)
        {
            var player = Players.FirstOrDefault(x => x.ConnectionId == connectionId);
            if (player is null)
                return false;

            Players.Remove(player);
            return true;
        }
    }

    public void AddChat(string nickname, string message)
    {
        lock (_lock)
        {
            ChatLog.Add(new ChatMessage
            {
                Nickname = nickname,
                Message = message,
                SentAtUtc = DateTime.UtcNow
            });
        }
    }

    public GameActionResult SubmitAction(string connectionId, string actionType, string payload)
    {
        lock (_lock)
        {
            var player = Players.FirstOrDefault(x => x.ConnectionId == connectionId);
            if (player is null)
            {
                return new GameActionResult
                {
                    Success = false,
                    Message = "Player not found.",
                    PublicState = CurrentGame.GetPublicState()
                };
            }

            return CurrentGame.SubmitAction(player, actionType, payload);
        }
    }

    public object GetPublicState()
    {
        lock (_lock)
        {
            return new
            {
                SessionCode,
                PlayerCount = Players.Count,
                Players = Players.Select(x => x.Nickname).ToList(),
                Game = CurrentGame.GetPublicState()
            };
        }
    }
}