using System.Text.Json;
using PVPBack.Core.Realtime.MiniGames.Games.Timeline;

namespace PVPBack.Core.Realtime.MiniGames;

/// <summary>
/// Timeline Game: 4 players collaborate to reconstruct a 16-step story timeline in chronological order.
/// </summary>
public class TimelineGame : IMiniGame
{
    private List<PlayerRuntime> _players = new();

    // Timeline cards - all 16 cards with their correct chronological order
    private List<TimelineCard> _allCards = new();

    // Correct order indices (0-15) - secret answer key
    private readonly List<int> _correctOrder = new();

    // Player hands: playerId -> list of card IDs in their hand
    private readonly Dictionary<string, List<string>> _playerHands = new();

    // Timeline slots: slot index (0-15) -> card ID (null if empty)
    private readonly Dictionary<int, string?> _timelineSlots = new();

    // Current lives
    private const int MaxLives = 3;
    private int _currentLives = MaxLives;

    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    // =====================================================
    // STORY DATA - Hardcoded 16-step narratives
    // =====================================================

    private static readonly List<StoryTemplate> StoryTemplates = new()
    {
        new StoryTemplate
        {
            Theme = "The Lost Artifact",
            Cards = new List<TimelineCard>
            {
                new("card_001", "The Discovery", "An archaeologist uncovers a mysterious artifact in the desert sands."),
                new("card_002", "Ancient Warning", "Hieroglyphics on the artifact speak of a forgotten curse."),
                new("card_003", "The Museum", "The artifact is transported to the national museum for study."),
                new("card_004", "Strange Dreams", "The lead researcher begins having vivid dreams about a distant temple."),
                new("card_005", "The Map", "A hidden compartment reveals a map to a remote mountain range."),
                new("card_006", "Expedition Launch", "A team assembles and travels to the indicated location."),
                new("card_007", "The Entrance", "They discover the entrance to an ancient underground temple."),
                new("card_008", "Traps Activated", "Pressure plates trigger ancient defense mechanisms."),
                new("card_009", "The Chamber", "Deep inside, they find a chamber filled with golden statues."),
                new("card_010", "The Choice", "They must decide: take the treasure or seal the chamber forever."),
                new("card_011", "The Sacrifice", "One team member volunteers to stay behind to seal the entrance."),
                new("card_012", "Escape", "The remaining team members escape as the temple collapses."),
                new("card_013", "Return Home", "They return home, forever changed by their experience."),
                new("card_014", "The Revelation", "Years later, they discover the artifact was only the first of many."),
                new("card_015", "New Mission", "A secret society recruits them for their next adventure."),
                new("card_016", "The Legacy Begins", "Their story becomes legend, inspiring future explorers.")
            }
        },
        new StoryTemplate
        {
            Theme = "Space Station Omega",
            Cards = new List<TimelineCard>
            {
                new("card_001", "Launch Day", "The Omega space station launches from Earth orbit."),
                new("card_002", "First Contact", "A mysterious signal is detected from deep space."),
                new("card_003", "Investigation", "The crew traces the signal to a nearby nebula."),
                new("card_004", "Derelict Ship", "They discover an ancient alien vessel drifting in the void."),
                new("card_005", "Boarding", "A small team boards the alien ship to investigate."),
                new("card_006", "The Artifact", "They find a perfectly preserved alien device aboard."),
                new("card_007", "Activation", "The device activates upon human contact."),
                new("card_008", "Distress Call", "A distress signal is broadcast to nearby systems."),
                new("card_009", "Alien Arrival", "An alien rescue vessel approaches the station."),
                new("card_010", "First Meeting", "Diplomatic contact is established with the alien visitors."),
                new("card_011", "Exchange", "Cultural artifacts are exchanged between species."),
                new("card_012", "The Warning", "The aliens warn of an approaching cosmic threat."),
                new("card_013", "Alliance Formed", "Earth joins an intergalactic alliance for protection."),
                new("card_014", "Defense Grid", "The alliance helps build a defensive shield around Earth."),
                new("card_015", "New Era", "Humanity enters a new era of space exploration."),
                new("card_016", "First Star Voyage", "The first human starship sets sail for distant galaxies.")
            }
        },
        new StoryTemplate
        {
            Theme = "The Midnight Courier",
            Cards = new List<TimelineCard>
            {
                new("card_001", "The Package", "A mysterious package arrives at the courier office."),
                new("card_002", "Encrypted Note", "A note inside warns: 'Deliver to the old lighthouse at midnight'."),
                new("card_003", "The Route", "The courier studies the coastal road on the map."),
                new("card_004", "Departure", "The motorcycle roars to life as the courier sets off."),
                new("card_005", "The Storm", "A torrential rain begins to fall across the coastline."),
                new("card_006", "The Detour", "A fallen tree blocks the main road, forcing a shortcut."),
                new("card_007", "The Shadow", "Tail lights appear in the rearview mirror - they're being followed."),
                new("card_008", "The Chase", "The courier accelerates, weaving through the storm."),
                new("card_009", "The Lighthouse", "The beacon comes into view through the rain."),
                new("card_010", "Arrival", "The courier parks and approaches the lighthouse door."),
                new("card_011", "The Handoff", "An old woman opens the door and accepts the package."),
                new("card_012", "The Truth", "Inside the package: a photograph revealing a family secret."),
                new("card_013", "The Reveal", "The woman explains the courier's true heritage."),
                new("card_014", "Inheritance", "The courier inherits the lighthouse and its mysteries."),
                new("card_015", "New Beginning", "A letter reveals a mission for the next generation."),
                new("card_016", "The Legacy", "The courier becomes the next guardian of the lighthouse.")
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

        // Deal 4 cards to each player
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var playerCards = shuffledCards.Skip(i * 4).Take(4).Select(c => c.Id).ToList();
            _playerHands[player.PlayerId] = playerCards;
        }

        // Initialize timeline slots (all empty)
        for (int i = 0; i < 16; i++)
        {
            _timelineSlots[i] = null;
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

        if (slotIndex < 0 || slotIndex >= 16)
        {
            return Failure("Slot index must be between 0 and 15.");
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
        _timelineSlots[slotIndex] = cardId;

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

        if (slotIndex < 0 || slotIndex >= 16)
        {
            return Failure("Slot index must be between 0 and 15.");
        }

        // Check if slot has a card
        var cardId = _timelineSlots[slotIndex];
        if (cardId is null)
        {
            return Failure($"Slot {slotIndex} is empty.");
        }

        // Return card to player's hand
        _timelineSlots[slotIndex] = null;
        _playerHands[player.PlayerId].Add(cardId);

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
        if (filledSlots < 16)
        {
            return Failure($"Cannot verify: only {filledSlots}/16 slots filled.");
        }

        // Verify the timeline order
        var isCorrect = true;
        var firstWrongSlot = -1;

        for (int slot = 0; slot < 16; slot++)
        {
            var cardId = _timelineSlots[slot];
            if (cardId is null) continue;

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
                .Select(c => new { c!.Id, c.Title, c.Description })
                .ToList();

            player.PrivateData = new
            {
                Hand = handCards,
                HandCount = handList.Count
            };
        }
    }

    // =====================================================
    // PUBLIC STATE
    // =====================================================

    public object GetPublicState()
    {
        var timeline = new List<object?>();
        for (int i = 0; i < 16; i++)
        {
            var cardId = _timelineSlots[i];
            if (cardId is null)
            {
                timeline.Add(null);
            }
            else
            {
                var card = _allCards.FirstOrDefault(c => c.Id == cardId);
                timeline.Add(card is null ? null : new { card.Id, card.Title, card.Description });
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
            TotalSlots = 16
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