using System.Text.Json;
using PVPBack.Core.Realtime.MiniGames.Games.Timeline;

namespace PVPBack.Core.Realtime.MiniGames;

/// <summary>
/// Timeline Game: 4 players collaborate to reconstruct a 12-step story timeline in chronological order.
/// </summary>
public class TimelineGame : IMiniGame
{
    private List<PlayerRuntime> _players = new();

    // Timeline cards - all 12 cards with their correct chronological order
    private List<TimelineCard> _allCards = new();

    // Correct order indices (0-11) - secret answer key
    private readonly List<int> _correctOrder = new();

    // Player hands: playerId -> list of card IDs in their hand
    private readonly Dictionary<string, List<string>> _playerHands = new();

    // Timeline slots: slot index (0-11) -> { cardId, ownerId }
    private readonly Dictionary<int, (string cardId, string ownerId)?> _timelineSlots = new();

    // Owner lookup: ownerId -> list of (slotIndex, card)
    private readonly Dictionary<string, List<(int slotIndex, TimelineCard card)>> _playerPlacedCards = new();

    // Current lives
    private const int MaxLives = 3;
    private int _currentLives = MaxLives;

    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    // Timeline size constants
    private const int TotalSlots = 12;
    private const int CardsPerPlayer = 3;
    private const int PlayerCount = 4;

    // =====================================================
    // STORY DATA - Single theme "Perspective" (12 cards, image names only)
    // Images located at: /games/Timeline/img/
    // =====================================================

    private static readonly StoryTemplate Story = new()
    {
        Theme = "Perspective",
        Cards = new List<TimelineCard>
        {
            new("card_001", "perspective_001"),
            new("card_002", "perspective_002"),
            new("card_003", "perspective_003"),
            new("card_004", "perspective_004"),
            new("card_005", "perspective_005"),
            new("card_006", "perspective_006"),
            new("card_007", "perspective_007"),
            new("card_008", "perspective_008"),
            new("card_009", "perspective_009"),
            new("card_010", "perspective_010"),
            new("card_011", "perspective_011"),
            new("card_012", "perspective_012")
        }
    };

    // =====================================================
    // START
    // =====================================================

    public void Start(List<PlayerRuntime> players)
    {
        _players = players;

        _allCards.Clear();
        _correctOrder.Clear();
        _playerHands.Clear();
        _timelineSlots.Clear();
        _playerPlacedCards.Clear();

        _currentLives = MaxLives;
        IsCompleted = false;
        IsFailed = false;

        // Initialize cards with their correct indices
        for (int i = 0; i < Story.Cards.Count; i++)
        {
            var card = Story.Cards[i];
            _allCards.Add(card);
            _correctOrder.Add(i);
        }

        // Shuffle cards for dealing
        var shuffledCards = _allCards.OrderBy(_ => Guid.NewGuid()).ToList();

        // Deal 3 cards to each player (4 players × 3 = 12 total)
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var playerCards = shuffledCards.Skip(i * CardsPerPlayer).Take(CardsPerPlayer).Select(c => c.Id).ToList();
            _playerHands[player.PlayerId] = playerCards;
        }

        // Initialize timeline slots (all empty) - 12 slots
        for (int i = 0; i < TotalSlots; i++)
        {
            _timelineSlots[i] = null;
        }

        // Initialize placed cards tracking for each player
        foreach (var player in players)
        {
            _playerPlacedCards[player.PlayerId] = new List<(int, TimelineCard)>();
        }

        RefreshPlayerPrivateData(players);
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    public GameActionResult SubmitAction(PlayerRuntime player, GameAction action)
    {
        if (IsCompleted || IsFailed)
        {
            return Failure("Game already ended.");
        }

        return action.Type switch
        {
            "place_card" => HandlePlaceCard(player, action.Data),
            "remove_card" => HandleRemoveCard(player, action.Data),
            "verify" => HandleVerify(player),
            _ => Failure("Unknown action type.")
        };
    }

    private GameActionResult HandlePlaceCard(PlayerRuntime player, JsonElement? data)
    {
        if (!_playerHands.ContainsKey(player.PlayerId))
        {
            return Failure("Player not found.");
        }

        if (data is null ||
            !data.Value.TryGetProperty("cardId", out var cardIdElement) ||
            cardIdElement.ValueKind != JsonValueKind.String)
        {
            return Failure("Invalid payload: cardId required.");
        }

        if (!data.Value.TryGetProperty("slotIndex", out var slotIndexElement) ||
            slotIndexElement.ValueKind != JsonValueKind.Number)
        {
            return Failure("Invalid payload: slotIndex required.");
        }

        var cardId = cardIdElement.GetString();
        var slotIndex = slotIndexElement.GetInt32();

        if (string.IsNullOrEmpty(cardId))
        {
            return Failure("Card ID cannot be empty.");
        }

        if (slotIndex < 0 || slotIndex >= TotalSlots)
        {
            return Failure($"Slot index must be between 0 and {TotalSlots - 1}.");
        }

        // Check if slot is already occupied
        if (_timelineSlots[slotIndex] is not null)
        {
            return Failure($"Slot {slotIndex} is already occupied.");
        }

        // Check if player has this card in hand
        if (!_playerHands[player.PlayerId].Contains(cardId))
        {
            return Failure("You don't have this card in your hand.");
        }

        // Remove from hand and place on timeline
        _playerHands[player.PlayerId].Remove(cardId);
        _timelineSlots[slotIndex] = (cardId, player.PlayerId);

        // Track placed card for the owner
        var card = _allCards.FirstOrDefault(c => c.Id == cardId);
        if (card != null)
        {
            if (!_playerPlacedCards.ContainsKey(player.PlayerId))
            {
                _playerPlacedCards[player.PlayerId] = new List<(int, TimelineCard)>();
            }
            _playerPlacedCards[player.PlayerId].Add((slotIndex, card));
        }

        return new GameActionResult
        {
            Success = true,
            Message = $"Card placed on slot {slotIndex}.",
            PublicState = GetPublicState()
        };
    }

    private GameActionResult HandleRemoveCard(PlayerRuntime player, JsonElement? data)
    {
        if (!_playerHands.ContainsKey(player.PlayerId))
        {
            return Failure("Player not found.");
        }

        if (data is null ||
            !data.Value.TryGetProperty("slotIndex", out var slotIndexElement) ||
            slotIndexElement.ValueKind != JsonValueKind.Number)
        {
            return Failure("Invalid payload: slotIndex required.");
        }

        var slotIndex = slotIndexElement.GetInt32();

        if (slotIndex < 0 || slotIndex >= TotalSlots)
        {
            return Failure($"Slot index must be between 0 and {TotalSlots - 1}.");
        }

        // Check if slot has a card
        var slotData = _timelineSlots[slotIndex];
        if (slotData is null)
        {
            return Failure($"Slot {slotIndex} is empty.");
        }

        // Check ownership - only the owner can remove their card
        var (cardId, ownerId) = slotData.Value;
        if (ownerId != player.PlayerId)
        {
            return Failure("You can only remove cards you placed.");
        }

        // Remove from timeline and return card to player's hand
        _timelineSlots[slotIndex] = null;
        _playerHands[player.PlayerId].Add(cardId);

        // Remove from player's placed cards
        if (_playerPlacedCards.ContainsKey(player.PlayerId))
        {
            _playerPlacedCards[player.PlayerId].RemoveAll(p => p.slotIndex == slotIndex);
        }

        return new GameActionResult
        {
            Success = true,
            Message = $"Card returned to your hand.",
            PublicState = GetPublicState()
        };
    }

    private GameActionResult HandleVerify(PlayerRuntime player)
    {
        // Check if all slots are filled
        var filledSlots = _timelineSlots.Values.Count(v => v is not null);
        if (filledSlots < TotalSlots)
        {
            return Failure($"Cannot verify: only {filledSlots}/{TotalSlots} slots filled.");
        }

        // Verify the timeline order
        var isCorrect = true;
        var firstWrongSlot = -1;

        for (int slot = 0; slot < TotalSlots; slot++)
        {
            var slotData = _timelineSlots[slot];
            if (!slotData.HasValue) continue;
            var cardId = slotData.Value.cardId;

            var card = _allCards.FirstOrDefault(c => c.Id == cardId);
            if (card is null) continue;

            // Find the correct position for this card
            var correctIndex = _correctOrder[_allCards.FindIndex(c => c.Id == cardId)];
            if (correctIndex != slot)
            {
                isCorrect = false;
                firstWrongSlot = slot;
                break;
            }
        }

        if (isCorrect)
        {
            IsCompleted = true;

            return new GameActionResult
            {
                Success = true,
                Message = "Timeline verified correctly! Game completed!",
                PublicState = GetPublicState(),
                UiMessage = new GameUiMessage
                {
                    Variant = "success",
                    Message = "Timeline correctly ordered! You win!"
                }
            };
        }

        // Failed verification
        _currentLives--;

        if (_currentLives <= 0)
        {
            IsFailed = true;
            IsCompleted = true;
        }

        var uiMessage = _currentLives > 0
            ? $"Wrong! Slot {firstWrongSlot} is incorrect. Lives remaining: {_currentLives}"
            : "Game over! The timeline was incorrect.";

        return new GameActionResult
        {
            Success = true,
            Message = IsFailed ? "Verification failed. Game over." : "Verification failed. Try again.",
            PublicState = GetPublicState(),
            UiMessage = new GameUiMessage
            {
                Variant = IsFailed ? "error" : "warning",
                Message = uiMessage
            }
        };
    }

    // =====================================================
    // PRIVATE DATA
    // =====================================================

    public void RefreshPlayerPrivateData(List<PlayerRuntime> players)
    {
        foreach (var player in _players)
        {
            _playerHands.TryGetValue(player.PlayerId, out var hand);

            // Get full card details for cards in hand
            var handList = hand ?? new List<string>();
            var handCards = handList
                .Select(cardId => _allCards.FirstOrDefault(c => c.Id == cardId))
                .Where(c => c is not null)
                .Select(c => new { c!.Id, c.ImageName })
                .ToList();

            // Get player's placed cards
            var placedCards = new List<object>();
            if (_playerPlacedCards.TryGetValue(player.PlayerId, out var placed))
            {
                foreach (var (slotIndex, card) in placed)
                {
                    placedCards.Add(new { SlotIndex = slotIndex, Card = new { card.Id, card.ImageName } });
                }
            }

            player.PrivateData = new
            {
                Hand = handCards,
                HandCount = handList.Count,
                PlacedCards = placedCards
            };
        }
    }

    // =====================================================
    // PUBLIC STATE
    // =====================================================

    public object GetPublicState()
    {
        var timeline = new List<object?>();
        for (int i = 0; i < TotalSlots; i++)
        {
            var slotData = _timelineSlots[i];
            if (slotData is null)
            {
                timeline.Add(null);
            }
            else
            {
                var (cardId, ownerId) = slotData.Value;
                var owner = _players.FirstOrDefault(p => p.PlayerId == ownerId);
                timeline.Add(new { IsFilled = true, OwnerId = ownerId, OwnerNickname = owner?.Nickname ?? "Unknown" });
            }
        }

        return new
        {
            GameType = "Timeline",
            Theme = Story.Theme,
            Status = IsFailed ? "failed" : IsCompleted ? "completed" : "running",
            Lives = _currentLives,
            MaxLives,
            Timeline = timeline,
            FilledSlots = _timelineSlots.Values.Count(v => v is not null),
            TotalSlots = TotalSlots
        };
    }

    // =====================================================
    // HELPERS
    // =====================================================

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