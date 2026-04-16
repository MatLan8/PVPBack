using System.Text.Json;
using PVPBack.Core.Realtime;
using PVPBack.Core.Realtime.MiniGames.Games.Connections;

namespace PVPBack.Core.Realtime.MiniGames;

public class ConnectionsGame : IMiniGame
{
    private readonly Dictionary<string, List<string>> _assignedWords = new();
    private readonly Dictionary<string, HashSet<string>> _playerSelections = new();
    private readonly Dictionary<string, bool> _playerReadyStates = new();

    private readonly List<ConnectionsGroupDefinition> _selectedGroups = new();
    private readonly List<ConnectionsGroupDefinition> _solvedGroups = new();

    private List<PlayerRuntime> _players = new();

    private const int MaxMistakes = 3;
    private int _mistakeCount = 0;

    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    public void Start(List<PlayerRuntime> players)
    {
        _players = players;

        _selectedGroups.Clear();
        _solvedGroups.Clear();
        _assignedWords.Clear();
        _playerSelections.Clear();
        _playerReadyStates.Clear();
        _mistakeCount = 0;
        IsCompleted = false;
        IsFailed = false;

        var chosenGroups = ConnectionsWordBank.All
            .OrderBy(_ => Guid.NewGuid())
            .Take(4)
            .Select(g => new ConnectionsGroupDefinition
            {
                Name = g.Name,
                Words = g.Words.ToList()
            })
            .ToList();

        _selectedGroups.AddRange(chosenGroups);

        var allWords = chosenGroups
            .SelectMany(g => g.Words)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        for (int i = 0; i < players.Count; i++)
        {
            var playerWords = allWords.Skip(i * 4).Take(4).ToList();

            _assignedWords[players[i].PlayerId] = playerWords;
            _playerSelections[players[i].PlayerId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _playerReadyStates[players[i].PlayerId] = false;
        }

        RefreshPlayerPrivateData(players);
    }

    public GameActionResult SubmitAction(PlayerRuntime player, GameAction action)
    {
        if (IsCompleted || IsFailed)
        {
            return new GameActionResult
            {
                Success = false,
                Message = "Game already ended.",
                PublicState = GetPublicState()
            };
        }

        return action.Type switch
        {
            "set_selection" => HandleSetSelection(player, action.Data),
            "set_ready" => HandleSetReady(player, action.Data),
            _ => new GameActionResult
            {
                Success = false,
                Message = "Unknown action type.",
                PublicState = GetPublicState()
            }
        };
    }

    public void RefreshPlayerPrivateData(List<PlayerRuntime> players)
    {
        foreach (var player in players)
        {
            _assignedWords.TryGetValue(player.PlayerId, out var visibleWords);
            _playerSelections.TryGetValue(player.PlayerId, out var selectedWords);

            player.PrivateData = new
            {
                VisibleWords = visibleWords ?? new List<string>(),
                SelectedWords = selectedWords?.ToList() ?? new List<string>()
            };
        }
    }

    public object GetPublicState()
    {
        return new
        {
            GameType = "Connections",
            Status = IsFailed ? "failed" : IsCompleted ? "completed" : "running",
            MistakeCount = _mistakeCount,
            MaxMistakes,
            SolvedGroups = _solvedGroups.Select(group => new
            {
                Name = group.Name,
                Words = group.Words.ToList()
            }).ToList(),
            Players = _playerReadyStates.Select(x => new
            {
                PlayerId = x.Key,
                IsReady = x.Value,
                SelectedCount = _playerSelections.TryGetValue(x.Key, out var selection) ? selection.Count : 0
            }).ToList()
        };
    }

    private GameActionResult HandleSetSelection(PlayerRuntime player, JsonElement? data)
    {
        if (!_assignedWords.ContainsKey(player.PlayerId))
        {
            return Failure("Player has no assigned words.");
        }

        if (_playerReadyStates[player.PlayerId])
        {
            return Failure("Player is ready. Unready first to change selection.");
        }

        if (data is null ||
            !data.Value.TryGetProperty("words", out var wordsElement) ||
            wordsElement.ValueKind != JsonValueKind.Array)
        {
            return Failure("Invalid selection payload.");
        }

        var allowedWords = _assignedWords[player.PlayerId];
        var newSelection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in wordsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return Failure("Selection contains invalid word.");

            var word = item.GetString();

            if (string.IsNullOrWhiteSpace(word))
                return Failure("Selection contains empty word.");

            if (!allowedWords.Contains(word, StringComparer.OrdinalIgnoreCase))
                return Failure("Player can only select from their own visible words.");

            newSelection.Add(word);
        }

        _playerSelections[player.PlayerId] = newSelection;

        return new GameActionResult
        {
            Success = true,
            Message = "Selection updated.",
            PublicState = GetPublicState()
        };
    }

    private GameActionResult HandleSetReady(PlayerRuntime player, JsonElement? data)
    {
        if (!_playerReadyStates.ContainsKey(player.PlayerId))
        {
            return Failure("Player not found in ready states.");
        }

        if (data is null ||
            !data.Value.TryGetProperty("isReady", out var isReadyElement) ||
            (isReadyElement.ValueKind != JsonValueKind.True && isReadyElement.ValueKind != JsonValueKind.False))
        {
            return Failure("Invalid ready payload.");
        }

        var isReady = isReadyElement.GetBoolean();
        _playerReadyStates[player.PlayerId] = isReady;

        if (!AllPlayersReady())
        {
            return new GameActionResult
            {
                Success = true,
                Message = isReady ? "Player is ready." : "Player is unready.",
                PublicState = GetPublicState()
            };
        }

        return ResolveTeamAttempt();
    }

    private GameActionResult ResolveTeamAttempt()
    {
        var combinedWords = _playerSelections
            .Values
            .SelectMany(x => x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (combinedWords.Count != 4)
        {
            RegisterMistake();

            return new GameActionResult
            {
                Success = true,
                Message = IsFailed
                    ? "Incorrect attempt. Team failed the game."
                    : "Incorrect attempt. Team must select exactly 4 words in total.",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "error",
                    Message = "Mistake! Select exactly 4 words total."
                }
            };
        }

        var matchedGroup = _selectedGroups.FirstOrDefault(group =>
            !_solvedGroups.Any(solved =>
                solved.Name.Equals(group.Name, StringComparison.OrdinalIgnoreCase))
            && group.Words.Count == 4
            && group.Words.All(word =>
                combinedWords.Contains(word, StringComparer.OrdinalIgnoreCase)));

        if (matchedGroup is not null)
        {
            _solvedGroups.Add(new ConnectionsGroupDefinition
            {
                Name = matchedGroup.Name,
                Words = matchedGroup.Words.ToList()
            });

            RemoveSolvedWordsFromPlayers(matchedGroup.Words);
            RefreshPlayerPrivateData(_players);
            ResetSelectionsAndReadyStates();

            if (_solvedGroups.Count == _selectedGroups.Count)
            {
                IsCompleted = true;
            }

            return new GameActionResult
            {
                Success = true,
                Message = IsCompleted ? "Correct group. Game completed." : "Correct group.",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "success",
                    Message = IsCompleted ? "Correct group. Game completed." : "Correct group!"
                }
            };
        }

        var bestOverlap = GetBestOverlapCount(combinedWords);

        RegisterMistake();

        return new GameActionResult
        {
            Success = true,
            Message = IsFailed
                ? "Incorrect group. Team failed the game."
                : "Incorrect group.",
            PublicState = GetPublicState(),
            UiMessage = new GameUiMessage
            {
                Variant = bestOverlap == 3 ? "warning" : "error",
                Message = bestOverlap == 3 ? "Mistake! One word off" : "Mistake! Wrong group"
            }
        };
    }

    private int GetBestOverlapCount(List<string> combinedWords)
    {
        var combinedSet = new HashSet<string>(combinedWords, StringComparer.OrdinalIgnoreCase);

        return _selectedGroups
            .Where(group => !_solvedGroups.Any(solved =>
                solved.Name.Equals(group.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(group => group.Words.Count(word => combinedSet.Contains(word)))
            .DefaultIfEmpty(0)
            .Max();
    }

    private void RegisterMistake()
    {
        _mistakeCount++;
        ResetSelectionsAndReadyStates();

        if (_mistakeCount >= MaxMistakes)
        {
            IsFailed = true;
        }
    }

    private void RemoveSolvedWordsFromPlayers(List<string> solvedWords)
    {
        foreach (var playerId in _assignedWords.Keys.ToList())
        {
            _assignedWords[playerId] = _assignedWords[playerId]
                .Where(word => !solvedWords.Contains(word, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void ResetSelectionsAndReadyStates()
    {
        foreach (var playerId in _playerSelections.Keys.ToList())
        {
            _playerSelections[playerId].Clear();
        }

        foreach (var playerId in _playerReadyStates.Keys.ToList())
        {
            _playerReadyStates[playerId] = false;
        }

        RefreshPlayerPrivateData(_players);
    }

    private bool AllPlayersReady()
    {
        return _playerReadyStates.Count > 0 && _playerReadyStates.Values.All(x => x);
    }

    private GameActionResult Failure(string message)
    {
        return new GameActionResult
        {
            Success = false,
            Message = message,
            PublicState = GetPublicState()
        };
    }
}