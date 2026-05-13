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
    // STORY DATA - Chained 12-step narratives (each card hints at next)
    // =====================================================

    private static readonly List<StoryTemplate> StoryTemplates = new()
    {
        new StoryTemplate
            {
                Theme = "The Treasure Hunt",
                Cards = new List<TimelineCard>
                {
                    new("card_001", "Start: Buying Tools", "First, the explorer buys a shovel at the store.", "https://picsum.photos/seed/shovel/400/300"),
                    new("card_002", "The Desert", "Next, the explorer travels to the hot desert.", "https://picsum.photos/seed/desert/400/300"),
                    new("card_003", "Digging", "The explorer starts digging a deep hole in the sand.", "https://picsum.photos/seed/digging/400/300"),
                    new("card_004", "A Hard Hit", "Suddenly, the shovel hits a hard, wooden box.", "https://picsum.photos/seed/hit_box/400/300"),
                    new("card_005", "Pulling it Out", "The explorer pulls the dirty box out of the hole.", "https://picsum.photos/seed/pull_box/400/300"),
                    new("card_006", "Cleaning", "Using a brush, the explorer cleans the dirt off the box.", "https://picsum.photos/seed/brush/400/300"),
                    new("card_007", "Unlocking", "The explorer unlocks the box with a rusty key.", "https://picsum.photos/seed/unlock/400/300"),
                    new("card_008", "The Map", "Inside the box, there is an old treasure map.", "https://picsum.photos/seed/treasure_map/400/300"),
                    new("card_009", "The Cave", "The map leads the explorer to a hidden cave.", "https://picsum.photos/seed/easy_cave/400/300"),
                    new("card_010", "The Treasure", "Walking into the cave, the explorer spots a gold statue.", "https://picsum.photos/seed/statue/400/300"),
                    new("card_011", "Packing Up", "The explorer carefully puts the statue in a backpack.", "https://picsum.photos/seed/backpack/400/300"),
                    new("card_012", "The Museum", "Finally, the explorer gives the gold statue to a museum.", "https://picsum.photos/seed/easy_museum/400/300")
                }
            },
            new StoryTemplate
            {
                Theme = "The Friendly Aliens",
                Cards = new List<TimelineCard>
                {
                    new("card_001", "Start: Building", "First, astronauts build a big rocket on Earth.", "https://picsum.photos/seed/build_rocket/400/300"),
                    new("card_002", "Blast Off", "The rocket blasts off high into outer space.", "https://picsum.photos/seed/blastoff/400/300"),
                    new("card_003", "Docking", "The rocket safely parks at the space station.", "https://picsum.photos/seed/docking/400/300"),
                    new("card_004", "Looking Out", "An astronaut looks through the station's window.", "https://picsum.photos/seed/window/400/300"),
                    new("card_005", "A Ship Appears", "They spot a glowing alien spaceship flying toward them.", "https://picsum.photos/seed/ufo/400/300"),
                    new("card_006", "Parking", "The alien spaceship parks right next to the station.", "https://picsum.photos/seed/park_ufo/400/300"),
                    new("card_007", "Doors Open", "The airlock doors open to let the visitors inside.", "https://picsum.photos/seed/doors_open/400/300"),
                    new("card_008", "Meeting Aliens", "Friendly, green aliens step out of their ship.", "https://picsum.photos/seed/green_aliens/400/300"),
                    new("card_009", "Saying Hello", "The aliens wave and say 'Hello' in English.", "https://picsum.photos/seed/wave_hello/400/300"),
                    new("card_010", "Eating Pizza", "The astronauts give the aliens some Earth pizza to eat.", "https://picsum.photos/seed/space_pizza/400/300"),
                    new("card_011", "A Gift", "The aliens give the astronauts a glowing rock as a gift.", "https://picsum.photos/seed/gift_rock/400/300"),
                    new("card_012", "Group Photo", "Finally, everyone takes a happy group photo together.", "https://picsum.photos/seed/space_photo/400/300")
                }
            },
            new StoryTemplate
            {
                Theme = "The Lighthouse Delivery",
                Cards = new List<TimelineCard>
                {
                    new("card_001", "Start: The Post Office", "First, the mailman picks up a package at the post office.", "https://picsum.photos/seed/post_office/400/300"),
                    new("card_002", "Reading the Label", "He reads the label: 'Deliver to the tall lighthouse.'", "https://picsum.photos/seed/read_label/400/300"),
                    new("card_003", "Riding the Bike", "The mailman gets on his bicycle to start the trip.", "https://picsum.photos/seed/ride_bike/400/300"),
                    new("card_004", "Down the Road", "He pedals quickly down the dirt road toward the beach.", "https://picsum.photos/seed/dirt_road/400/300"),
                    new("card_005", "It Starts Raining", "It starts raining, so he puts on his yellow raincoat.", "https://picsum.photos/seed/yellow_coat/400/300"),
                    new("card_006", "The Big Hill", "He rides up a very big, steep hill.", "https://picsum.photos/seed/big_hill/400/300"),
                    new("card_007", "Reaching the Top", "At the top of the hill, he sees the tall lighthouse.", "https://picsum.photos/seed/see_lighthouse/400/300"),
                    new("card_008", "Walking Up", "He parks his bike and walks up the front steps.", "https://picsum.photos/seed/front_steps/400/300"),
                    new("card_009", "Knocking", "He knocks loudly on the heavy wooden door.", "https://picsum.photos/seed/knock_door/400/300"),
                    new("card_010", "The Sailor", "An old sailor opens the door with a big smile.", "https://picsum.photos/seed/old_sailor/400/300"),
                    new("card_011", "Handing it Over", "The mailman hands the dry package to the sailor.", "https://picsum.photos/seed/hand_package/400/300"),
                    new("card_012", "Opening the Box", "Finally, the sailor opens the package and finds a new compass.", "https://picsum.photos/seed/new_compass/400/300")
                }
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

        // Select a random story template
        var selectedStory = StoryTemplates[Random.Shared.Next(StoryTemplates.Count)];

        // Initialize cards with their correct indices
        for (int i = 0; i < selectedStory.Cards.Count; i++)
        {
            var card = selectedStory.Cards[i];
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
        foreach (var player in players)
        {
            _playerHands.TryGetValue(player.PlayerId, out var hand);

            // Get full card details for cards in hand
            var handList = hand ?? new List<string>();
            var handCards = handList
                .Select(cardId => _allCards.FirstOrDefault(c => c.Id == cardId))
                .Where(c => c is not null)
                .Select(c => new { c!.Id, c.Title, c.Description, c.ImageUrl })
                .ToList();

            // Get player's placed cards
            var placedCards = new List<object>();
            if (_playerPlacedCards.TryGetValue(player.PlayerId, out var placed))
            {
                foreach (var (slotIndex, card) in placed)
                {
                    placedCards.Add(new { SlotIndex = slotIndex, Card = new { card.Id, card.Title, card.Description, card.ImageUrl } });
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