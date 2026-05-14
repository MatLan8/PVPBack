using System.Text.Json;
using PVPBack.Core.Realtime.MiniGames.Games.CodeBreakers;

namespace PVPBack.Core.Realtime.MiniGames;

public class CodeBreakersGame : IMiniGame
{
    private List<PlayerRuntime> _players = new();

    private readonly Dictionary<string, string> _playerHints = new();
    private readonly Dictionary<string, string> _submittedCodes = new();
    private readonly Dictionary<string, bool> _playerReadyStates = new();

    private CodePuzzleDefinition _activePuzzle = null!;

    private const int MaxAttempts = 3;
    private int _mistakeCount;

    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    // =====================================================
    // START
    // =====================================================

    public void Start(List<PlayerRuntime> players)
    {
        _players = players;

        _playerHints.Clear();
        _submittedCodes.Clear();
        _playerReadyStates.Clear();

        IsCompleted = false;
        IsFailed = false;

        _mistakeCount = 0;

        // Pick random puzzle
        var random = new Random();
        _activePuzzle = CodeBreakersPuzzleBank.Puzzles[
            random.Next(CodeBreakersPuzzleBank.Puzzles.Count)
        ];

        // Shuffle hints
        var shuffledHints = _activePuzzle.Hints
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        // Assign hints
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];

            _playerHints[player.PlayerId] =
                shuffledHints[i % shuffledHints.Count];

            _submittedCodes[player.PlayerId] = "";

            _playerReadyStates[player.PlayerId] = false;
        }

        RefreshPlayerPrivateData(players);
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    public GameActionResult SubmitAction(PlayerRuntime player, GameAction action)
    {
        if (IsCompleted || IsFailed)
            return Fail("Game already ended.");

        return action.Type switch
        {
            "submit_code" => HandleSubmitCode(player, action.Data),
            "set_ready" => HandleSetReady(player),
            _ => Fail("Unknown action.")
        };
    }

    private GameActionResult HandleSubmitCode(PlayerRuntime player, JsonElement? data)
    {
        if (data is null)
            return Fail("Invalid payload.");

        if (!data.Value.TryGetProperty("code", out var codeProp))
            return Fail("Missing code.");

        var code = codeProp.GetString()?.Trim();

        if (string.IsNullOrWhiteSpace(code))
            return Fail("Code is required.");

        if (code.Length != 4 || !code.All(char.IsDigit))
            return Fail("Code must contain exactly 4 digits.");

        // Save submitted code
        _submittedCodes[player.PlayerId] = code;

        // If player edits code after being ready,
        // automatically mark them unready
        _playerReadyStates[player.PlayerId] = false;

        RefreshPlayerPrivateData(_players);

        return new GameActionResult
        {
            Success = true,
            Message = "Code updated.",
            PublicState = GetPublicState()
        };
    }

    private GameActionResult HandleSetReady(PlayerRuntime player)
    {
        // Toggle ready state
        if (_playerReadyStates[player.PlayerId])
        {
            _playerReadyStates[player.PlayerId] = false;

            RefreshPlayerPrivateData(_players);

            return new GameActionResult
            {
                Success = true,
                Message = "Player unready.",
                PublicState = GetPublicState()
            };
        }

        // Require submitted code before ready
        if (string.IsNullOrWhiteSpace(_submittedCodes[player.PlayerId]))
        {
            return new GameActionResult
            {
                Success = false,
                Message = "Submit code first.",
                PublicState = GetPublicState()
            };
        }

        _playerReadyStates[player.PlayerId] = true;

        RefreshPlayerPrivateData(_players);

        // Wait until everyone ready
        if (_playerReadyStates.Values.Any(x => !x))
        {
            return new GameActionResult
            {
                Success = true,
                Message = "Player ready.",
                PublicState = GetPublicState()
            };
        }

        // =====================================================
        // ALL PLAYERS READY -> EVALUATE TEAM ATTEMPT
        // =====================================================

        var distinctCodes = _submittedCodes.Values
            .Distinct()
            .ToList();

        // =====================================================
        // PLAYERS SUBMITTED DIFFERENT CODES
        // =====================================================

        if (distinctCodes.Count != 1)
        {
            _mistakeCount++;

            ResetReadyStates();

            if (_mistakeCount >= MaxAttempts)
            {
                IsFailed = true;

                return new GameActionResult
                {
                    Success = true,
                    Message = "Game failed.",
                    PublicState = GetPublicState(),
                    UiMessage = new GameUiMessage
                    {
                        Variant = "error",
                        Message = $"Team failed. Correct code was {_activePuzzle.Code}."
                    }
                };
            }

            return new GameActionResult
            {
                Success = true,
                Message = "Players submitted different codes.",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "error",
                    Message = "Not all players submitted the same code."
                }
            };
        }

        var teamCode = distinctCodes[0];

        // =====================================================
        // CORRECT CODE
        // =====================================================

        if (teamCode == _activePuzzle.Code)
        {
            IsCompleted = true;

            return new GameActionResult
            {
                Success = true,
                Message = "Correct code!",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "success",
                    Message = "Code cracked successfully!"
                }
            };
        }

        // =====================================================
        // WRONG CODE
        // =====================================================

        _mistakeCount++;

        int correctDigits = 0;

        for (int i = 0; i < 4; i++)
        {
            if (teamCode[i] == _activePuzzle.Code[i])
                correctDigits++;
        }

        ResetReadyStates();

        if (_mistakeCount >= MaxAttempts)
        {
            IsFailed = true;

            return new GameActionResult
            {
                Success = true,
                Message = "Game failed.",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "error",
                    Message = $"Team failed. Correct code was {_activePuzzle.Code}."
                }
            };
        }

        return new GameActionResult
        {
            Success = true,
            Message = "Wrong code.",
            PublicState = GetPublicState(),
            UiMessage = new GameUiMessage
            {
                Variant = "warning",
                Message = $"{correctDigits}/4 digits were correct."
            }
        };
    }

    // =====================================================
    // PRIVATE DATA
    // =====================================================

    public void RefreshPlayerPrivateData(List<PlayerRuntime> players)
    {
        foreach (var p in players)
        {
            p.PrivateData = new
            {
                Hint = _playerHints[p.PlayerId],
                SubmittedCode = _submittedCodes[p.PlayerId],
                IsReady = _playerReadyStates[p.PlayerId]
            };
        }
    }

    // =====================================================
    // PUBLIC STATE
    // =====================================================

    public object GetPublicState()
    {
        return new
        {
            GameType = "CodeBreakers",

            Status = IsFailed
                ? "failed"
                : IsCompleted
                    ? "completed"
                    : "running",

            MaxAttempts,
            MistakeCount = _mistakeCount,

            Players = _playerReadyStates.Select(x => new
            {
                PlayerId = x.Key,
                IsReady = x.Value
            }).ToList()
        };
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private void ResetReadyStates()
    {
        foreach (var playerId in _playerReadyStates.Keys.ToList())
        {
            _playerReadyStates[playerId] = false;
        }
    }

    private GameActionResult Fail(string msg)
    {
        return new GameActionResult
        {
            Success = false,
            Message = msg,
            PublicState = GetPublicState()
        };
    }
}