using PVPBack.Core.Realtime.MiniGames;

namespace PVPBack.Core.Realtime;

public class GameSessionRuntime
{
    private readonly object _lock = new();

    private readonly List<IMiniGame> _games;
    private int _activeGameIndex;

    public string SessionCode { get; }
    public Guid DbSessionId { get; }
    public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;

    public List<PlayerRuntime> Players { get; } = new();
    public List<ChatMessage> ChatLog { get; } = new();

    /// <summary>0-based index of the round currently in play (meaningful after <see cref="HasStarted"/>).</summary>
    public int ActiveGameIndex => _activeGameIndex;

    public IReadOnlyList<IMiniGame> Games => _games;

    /// <summary>The mini-game that currently receives actions and timer/game-end checks.</summary>
    public IMiniGame ActiveGame => _games[_activeGameIndex];

    public bool HasStarted { get; private set; }

    public bool IsCompleted { get; private set; }
    public bool IsFinalized { get; private set; }

    public GameSessionRuntime(string sessionCode, Guid dbSessionId, IEnumerable<IMiniGame>? games = null)
    {
        SessionCode = sessionCode;
        DbSessionId = dbSessionId;

        _games = (games ?? MiniGamePipeline.CreateDefaultPipeline()).ToList();
        if (_games.Count == 0)
            throw new ArgumentException("At least one mini-game is required.", nameof(games));

        _activeGameIndex = 0;
    }

    public void MarkCompleted()
    {
        IsCompleted = true;
    }

    public bool TryFinalize()
    {
        if (IsFinalized) return false;

        IsFinalized = true;
        return true;
    }

    /// <summary>True when the active round failed, or the final round completed successfully.</summary>
    public bool IsSessionPlayFinished()
    {
        if (ActiveGame.IsFailed)
            return true;

        return ActiveGame.IsCompleted && _activeGameIndex >= _games.Count - 1;
    }

    /// <summary>True when the session ended in a full success (all rounds completed, none failed).</summary>
    public bool IsSessionSuccessful()
    {
        return ActiveGame.IsCompleted
               && !ActiveGame.IsFailed
               && _activeGameIndex >= _games.Count - 1;
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
                ActiveGame.Start(Players);
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
                    PublicState = ActiveGame.GetPublicState()
                };
            }

            var result = ActiveGame.SubmitAction(player, action);
            ActiveGame.RefreshPlayerPrivateData(Players);

            // Chain rounds: when the current game ends in success and another round exists, start it immediately.
            while (result.Success
                   && ActiveGame.IsCompleted
                   && !ActiveGame.IsFailed
                   && _activeGameIndex < _games.Count - 1)
            {
                _activeGameIndex++;
                ActiveGame.Start(Players);
                ActiveGame.RefreshPlayerPrivateData(Players);
            }

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
                RoundIndex = HasStarted ? _activeGameIndex : (int?)null,
                TotalRounds = HasStarted ? _games.Count : (int?)null,
                Game = HasStarted ? ActiveGame.GetPublicState() : null
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