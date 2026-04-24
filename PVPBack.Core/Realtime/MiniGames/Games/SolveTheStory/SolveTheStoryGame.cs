using System.Text.Json;
using PVPBack.Core.Realtime;
using PVPBack.Core.Realtime.MiniGames;
using PVPBack.Core.Realtime.MiniGames.Interface;

namespace PVPBack.Core.Realtime.MiniGames.Games.SolveTheStory;

public class TimelineSlot
{
    public string? CardId { get; set; }
    public string? PlayerId { get; set; }
}

public class SolveTheStoryGame : IMiniGame
{
    private const int TotalSlots = 16;
    private const int InitialLives = 3;
    private const int CardsPerPlayer = 4;

    private List<PlayerRuntime> _players = new();
    private StoryData _story = null!;
    private TimelineSlot[] _timeline = new TimelineSlot[16];
    private Dictionary<string, List<string>> _playerHands = new();
    private int _lives = InitialLives;
    private bool _isCompleted;
    private bool _isFailed;

    public bool IsCompleted => _isCompleted;
    public bool IsFailed => _isFailed;

    public void Start(List<PlayerRuntime> players)
    {
        _players = players;
        _lives = InitialLives;
        _isCompleted = false;
        _isFailed = false;

        _story = StoryLibrary.GetDefaultStory();

        for (int i = 0; i < TotalSlots; i++)
        {
            _timeline[i] = new TimelineSlot();
        }

        var allCardIds = _story.AllCards.Select(c => c.Id).OrderBy(_ => Guid.NewGuid()).ToList();

        _playerHands.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            var playerHand = allCardIds.Skip(i * CardsPerPlayer).Take(CardsPerPlayer).ToList();
            _playerHands[players[i].PlayerId] = playerHand;
        }

        RefreshPlayerPrivateData(players);
    }

    public GameActionResult SubmitAction(PlayerRuntime player, GameAction action)
    {
        if (_isCompleted || _isFailed)
            return Failure("Game is already over.");

        if (action.Type == "place_card")
            return HandlePlaceCard(player, action.Data);

        if (action.Type == "remove_card")
            return HandleRemoveCard(player, action.Data);

        if (action.Type == "verify_timeline")
            return HandleVerifyTimeline();

        return Failure("Unknown action type.");
    }

    private GameActionResult HandlePlaceCard(PlayerRuntime player, JsonElement? data)
    {
        if (!data.HasValue)
            return Failure("Missing action data.");

        if (!data.Value.TryGetProperty("slot", out var slotElement))
            return Failure("Missing slot index.");
        int slotIndex = slotElement.GetInt32();

        if (!data.Value.TryGetProperty("cardId", out var cardIdElement))
            return Failure("Missing card ID.");
        string cardId = cardIdElement.GetString()!;

        if (slotIndex < 0 || slotIndex >= TotalSlots)
            return Failure($"Invalid slot. Must be 0-{TotalSlots - 1}.");

        var playerHand = _playerHands.GetValueOrDefault(player.PlayerId) ?? new List<string>();
        if (!playerHand.Contains(cardId))
            return Failure("You don't have this card.");

        var currentSlot = _timeline[slotIndex];

        if (currentSlot.CardId != null)
        {
            var existingCardId = currentSlot.CardId;
            var existingPlayerId = currentSlot.PlayerId;

            _timeline[slotIndex] = new TimelineSlot
            {
                CardId = cardId,
                PlayerId = player.PlayerId
            };

            if (existingPlayerId != null && _playerHands.ContainsKey(existingPlayerId))
            {
                _playerHands[existingPlayerId].Add(existingCardId);
            }

            _playerHands[player.PlayerId].Remove(cardId);

            RefreshPlayerPrivateData(_players);
            return Ok("Card swapped on timeline.");
        }

        _timeline[slotIndex] = new TimelineSlot
        {
            CardId = cardId,
            PlayerId = player.PlayerId
        };

        _playerHands[player.PlayerId].Remove(cardId);

        RefreshPlayerPrivateData(_players);
        return Ok("Card placed on timeline.");
    }

    private GameActionResult HandleRemoveCard(PlayerRuntime player, JsonElement? data)
    {
        if (!data.HasValue)
            return Failure("Missing action data.");

        if (!data.Value.TryGetProperty("slot", out var slotElement))
            return Failure("Missing slot index.");
        int slotIndex = slotElement.GetInt32();

        if (slotIndex < 0 || slotIndex >= TotalSlots)
            return Failure($"Invalid slot. Must be 0-{TotalSlots - 1}.");

        var currentSlot = _timeline[slotIndex];
        if (string.IsNullOrEmpty(currentSlot.CardId))
            return Failure("Slot is already empty.");

        if (currentSlot.PlayerId != player.PlayerId)
            return Failure("You can only remove your own cards.");

        var cardId = currentSlot.CardId;
        _playerHands[player.PlayerId].Add(cardId);

        _timeline[slotIndex] = new TimelineSlot();

        RefreshPlayerPrivateData(_players);
        return Ok("Card returned to hand.");
    }

    private GameActionResult HandleVerifyTimeline()
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_timeline[i].CardId == null)
                return Failure($"Timeline incomplete: {i + 1}/16 slots filled.");
        }

        for (int i = 0; i < TotalSlots; i++)
        {
            var placedCardId = _timeline[i].CardId;
            var expectedCardId = _story.OrderedSolutionIds[i];

            if (placedCardId != expectedCardId)
            {
                _lives--;

                if (_lives <= 0)
                {
                    _isFailed = true;
                    return Failure("Timeline incorrect. No lives remaining. GAME OVER.");
                }

                return Failure($"Timeline incorrect at position {i + 1}. Lives remaining: {_lives}.");
            }
        }

        _isCompleted = true;
        return Ok("Timeline correct! YOU WIN!");
    }

    public void RefreshPlayerPrivateData(List<PlayerRuntime> players)
    {
        foreach (var p in players)
        {
            var hand = _playerHands.GetValueOrDefault(p.PlayerId) ?? new List<string>();

            var handCards = hand
                .Select(id => _story.AllCards.FirstOrDefault(c => c.Id == id))
                .Where(c => c != null)
                .Select(c => new { c!.Id, c.Title, c.Description })
                .ToList();

            var placedCards = new Dictionary<int, string>();
            for (int i = 0; i < TotalSlots; i++)
            {
                if (_timeline[i].PlayerId == p.PlayerId)
                {
                    placedCards[i] = _timeline[i].CardId!;
                }
            }

            p.PrivateData = new
            {
                Hand = handCards,
                PlacedCards = placedCards,
                Lives = _lives,
                MaxLives = InitialLives,
                TotalSlots,
                FilledSlots = _timeline.Count(s => s.CardId != null)
            };
        }
    }

    public object GetPublicState()
    {
        return new
        {
            GameType = "SolveTheStory",
            Status = _isCompleted ? "completed" : (_isFailed ? "failed" : "running"),
            StoryId = _story.StoryId,
            StoryName = _story.StoryName,
            Timeline = _timeline.Select((s, i) => new { Slot = i, s.CardId, s.PlayerId }).ToArray(),
            TotalSlots,
            FilledSlots = _timeline.Count(s => s.CardId != null),
            Lives = _lives,
            MaxLives = InitialLives,
            PlayerInfos = _players.Select(p => new
            {
                p.PlayerId,
                p.Nickname,
                PlacedCardCount = _timeline.Count(s => s.PlayerId == p.PlayerId)
            }).ToList()
        };
    }

    public void OnPlayerDisconnect(string playerId)
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            if (_timeline[i].PlayerId == playerId && _timeline[i].CardId != null)
            {
                _playerHands[playerId].Add(_timeline[i].CardId!);
                _timeline[i] = new TimelineSlot();
            }
        }

        RefreshPlayerPrivateData(_players);
    }

    private static GameActionResult Ok(string message) => new() { Success = true, Message = message };
    private static GameActionResult Failure(string message) => new() { Success = false, Message = message };
}