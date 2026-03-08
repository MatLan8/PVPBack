using System.Text.Json;

namespace PVPBack.Core.Realtime.MiniGames;

public class ConnectionsGame : IMiniGame
{
    private readonly Dictionary<string, List<string>> _assignedWords = new();
    private int _guessCount = 0;

    public bool IsCompleted { get; private set; }

    public void Start(List<PlayerRuntime> players)
    {
        var words = new List<string>
        {
            "Apple", "Banana", "Orange", "Mango",
            "Car", "Bus", "Train", "Plane",
            "Red", "Blue", "Green", "Yellow",
            "Cat", "Dog", "Bird", "Fish"
        };

        var shuffled = words.OrderBy(_ => Guid.NewGuid()).ToList();

        for (int i = 0; i < players.Count; i++)
        {
            var playerWords = shuffled.Skip(i * 4).Take(4).ToList();
            _assignedWords[players[i].PlayerId] = playerWords;

            players[i].PrivateData = new
            {
                VisibleWords = playerWords
            };
        }
    }

    public GameActionResult SubmitAction(PlayerRuntime player, string actionType, string payload)
    {
        if (actionType != "guess")
        {
            return new GameActionResult
            {
                Success = false,
                Message = "Unknown action type.",
                PublicState = GetPublicState()
            };
        }

        _guessCount++;

        var guessedWords = JsonSerializer.Deserialize<List<string>>(payload) ?? new List<string>();

        if (guessedWords.Count == 4 && _guessCount >= 3)
        {
            IsCompleted = true;
            return new GameActionResult
            {
                Success = true,
                Message = "Guess accepted. Game completed.",
                PublicState = GetPublicState()
            };
        }

        return new GameActionResult
        {
            Success = true,
            Message = "Guess received.",
            PublicState = GetPublicState()
        };
    }

    public object GetPublicState()
    {
        return new
        {
            Status = IsCompleted ? "completed" : "running",
            GuessCount = _guessCount
        };
    }
}