using System.Text.Json;

namespace PVPBack.Core.Realtime.MiniGames.Games.Wordle;

public class WordleGame : IMiniGame
{
    private List<PlayerRuntime> _players = new();

    private readonly HashSet<string> _wordBank = new();
    private string _chosenWord = null!;

    private const int MaxGuesses = 2;

    private readonly Dictionary<string, int> _remainingGuesses = new();
    private readonly Dictionary<string, List<GuessResult>> _playerGuesses = new();

    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    // =====================================================
    // START
    // =====================================================

    public void Start(List<PlayerRuntime> players)
    {
        _players = players;

        _wordBank.Clear();
        _remainingGuesses.Clear();
        _playerGuesses.Clear();

        IsCompleted = false;
        IsFailed = false;

        var path = Path.Combine(AppContext.BaseDirectory, "valid-wordle-words.txt");

        if (!File.Exists(path))
            throw new Exception("Word list file not found.");

        foreach (var line in File.ReadLines(path))
        {
            var word = line.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(word) && word.Length == 5)
                _wordBank.Add(word);
        }

        if (_wordBank.Count == 0)
            throw new Exception("Word bank is empty.");

        var random = new Random();
        _chosenWord = _wordBank.ElementAt(random.Next(_wordBank.Count));

        foreach (var p in players)
        {
            _remainingGuesses[p.PlayerId] = MaxGuesses;
            _playerGuesses[p.PlayerId] = new List<GuessResult>();
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

        if (action.Type != "guess")
            return Fail("Unknown action type.");

        var result = HandleGuess(player, action.Data);

        RefreshPlayerPrivateData(_players);

        return result;
    }

    private GameActionResult HandleGuess(PlayerRuntime player, JsonElement? data)
    {
        if (!_remainingGuesses.TryGetValue(player.PlayerId, out var remainingGuess))
            return Fail("Invalid player.");

        if (remainingGuess <= 0)
            return Fail("No guesses left.");

        if (data is null || !data.Value.TryGetProperty("word", out var wordProp))
            return Fail("Invalid payload.");

        var guess = wordProp.GetString()!.ToLower();

        if (guess.Length != 5)
            return Fail("Word must be 5 letters.");

        // ✅ Validate BEFORE consuming attempt
        if (!_wordBank.Contains(guess))
        {
            return new GameActionResult
            {
                Success = false,
                Message = "Invalid word.",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "error",
                    Message = "Word not in dictionary."
                }
            };
        }

        // Consume guess
        _remainingGuesses[player.PlayerId]--;

        // ✅ WIN
        if (guess == _chosenWord)
        {
            var completeStates = Enumerable.Repeat(LetterState.Correct, 5).ToArray();

            _playerGuesses[player.PlayerId].Add(new GuessResult
            {
                Word = guess,
                States = completeStates
            });

            IsCompleted = true;

            return new GameActionResult
            {
                Success = true,
                Message = "Correct word!",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "success",
                    Message = "Word guessed correctly!"
                }
            };
        }

        // =====================================================
        // WORDLE EVALUATION (CORRECT + PRESENT)
        // =====================================================

        var states = new LetterState[5];
        var target = _chosenWord.ToCharArray();
        var guessChars = guess.ToCharArray();

        var used = new bool[5];

        // Pass 1: Correct
        for (int i = 0; i < 5; i++)
        {
            if (guessChars[i] == target[i])
            {
                states[i] = LetterState.Correct;
                used[i] = true;
            }
        }

        // Pass 2: Present
        for (int i = 0; i < 5; i++)
        {
            if (states[i] == LetterState.Correct)
                continue;

            states[i] = LetterState.Absent;

            for (int j = 0; j < 5; j++)
            {
                if (used[j]) continue;

                if (guessChars[i] == target[j])
                {
                    states[i] = LetterState.Present;
                    used[j] = true;
                    break;
                }
            }
        }

        _playerGuesses[player.PlayerId].Add(new GuessResult
        {
            Word = guess,
            States = states
        });

        // =====================================================
        // FAIL CONDITION
        // =====================================================

        if (_remainingGuesses.Values.All(x => x == 0))
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
                    Message = $"No guesses left. Word was: {_chosenWord}"
                }
            };
        }

        return new GameActionResult
        {
            Success = true,
            Message = "Guess submitted.",
            PublicState = GetPublicState()
        };
    }

    // =====================================================
    // PRIVATE DATA
    // =====================================================

    public void RefreshPlayerPrivateData(List<PlayerRuntime> players)
    {
        foreach (var p in players)
        {
            _remainingGuesses.TryGetValue(p.PlayerId, out var remaining);
            _playerGuesses.TryGetValue(p.PlayerId, out var guesses);

            p.PrivateData = new
            {
                RemainingGuesses = remaining,
                Guesses = guesses
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
            GameType = "Wordle",
            Status = IsFailed ? "failed" : IsCompleted ? "completed" : "running",

            Players = _players.Select(p => new
            {
                p.PlayerId,
                RemainingGuesses = _remainingGuesses[p.PlayerId]
            })
        };
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private GameActionResult Fail(string msg)
        => new()
        {
            Success = false,
            Message = msg,
            PublicState = GetPublicState()
        };
}


// =====================================================
// DTOs
// =====================================================

public class GuessResult
{
    public string Word { get; set; } = null!;
    public LetterState[] States { get; set; } = new LetterState[5];
}

public enum LetterState
{
    Absent,   // gray
    Present,  // yellow
    Correct   // green  
}