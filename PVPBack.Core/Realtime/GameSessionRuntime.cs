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

    public bool HasStarted { get; private set; }

    public GameSessionRuntime(string sessionCode, Guid dbSessionId)
    {
        SessionCode = sessionCode;
        DbSessionId = dbSessionId;
        CurrentGame = new ConnectionsGame();
    }

    public PlayerRuntime AddOrReconnectPlayer(string playerId, string connectionId, string nickname)
    {
        lock (_lock)
        {
            var existingByPlayerId = Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (existingByPlayerId is not null)
            {
                existingByPlayerId.Reconnect(connectionId, nickname);
                return existingByPlayerId;
            }

            if (HasStarted)
                throw new InvalidOperationException("Game already started. New players cannot join.");

            if (Players.Count >= 4)
                throw new InvalidOperationException("Session is full.");

            var nicknameTaken = Players.Any(p =>
                p.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase) &&
                p.PlayerId != playerId);

            if (nicknameTaken)
                throw new InvalidOperationException("Nickname already taken in this session.");

            var player = new PlayerRuntime(playerId, connectionId, nickname);
            Players.Add(player);

            if (Players.Count == 4 && !HasStarted)
            {
                CurrentGame.Start(Players);
                HasStarted = true;
            }

            return player;
        }
    }

    public bool MarkDisconnected(string connectionId)
    {
        lock (_lock)
        {
            var player = Players.FirstOrDefault(x => x.ConnectionId == connectionId);
            if (player is null)
                return false;

            player.MarkDisconnected();
            return true;
        }
    }

    public ChatMessage AddChat(string connectionId, string message)
    {
        lock (_lock)
        {
            var player = Players.FirstOrDefault(x => x.ConnectionId == connectionId);
            if (player is null)
                throw new InvalidOperationException("Player not found.");

            var chatMessage = new ChatMessage
            {
                PlayerId = player.PlayerId,
                Nickname = player.Nickname,
                Message = message,
                SentAtUtc = DateTime.UtcNow
            };

            ChatLog.Add(chatMessage);
            return chatMessage;
        }
    }

    public List<ChatMessage> GetChatHistory()
    {
        lock (_lock)
        {
            return ChatLog
                .Select(x => new ChatMessage
                {
                    PlayerId = x.PlayerId,
                    Nickname = x.Nickname,
                    Message = x.Message,
                    SentAtUtc = x.SentAtUtc
                })
                .ToList();
        }
    }

    public GameActionResult SubmitAction(string connectionId, GameAction action)
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

            var result = CurrentGame.SubmitAction(player, action);

            CurrentGame.RefreshPlayerPrivateData(Players);

            return result;
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
                Players = Players.Select(x => new
                {
                    x.PlayerId,
                    x.Nickname,
                    x.IsConnected
                }).ToList(),
                HasStarted,
                Game = HasStarted ? CurrentGame.GetPublicState() : null
            };
        }
    }

    public object GetWaitingRoomState()
    {
        lock (_lock)
        {
            return new
            {
                sessionCode = SessionCode,
                players = Players.Select(x => x.Nickname).ToList()
            };
        }
    }
}