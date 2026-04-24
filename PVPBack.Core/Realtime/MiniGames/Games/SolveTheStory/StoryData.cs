namespace PVPBack.Core.Realtime.MiniGames.Games.SolveTheStory;

public class StoryCard
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
}

public static class StoryLibrary
{
    private static readonly StoryData _defaultStory = new StoryData
    {
        StoryId = "operation_nightfall",
        StoryName = "Operation: Nightfall",
        OrderedSolutionIds = new string[16]
        {
            "c1", "c2", "c3", "c4",
            "c5", "c6", "c7", "c8",
            "c9", "c10", "c11", "c12",
            "c13", "c14", "c15", "c16"
        },
        AllCards = new List<StoryCard>
        {
            new() { Id = "c1", Title = "The Intercept", Description = "A encrypted transmission is intercepted by intelligence agencies." },
            new() { Id = "c2", Title = "Code Broken", Description = "Cryptographers decipher the message - it's a warning." },
            new() { Id = "c3", Title = "Threat Identified", Description = "The source is traced to a rogue general planning a coup." },
            new() { Id = "c4", Title = "Operation Greenlight", Description = "A covert team is assembled to stop the plot." },
            new() { Id = "c5", Title = "Deep Cover", Description = "Agents infiltrate the general's inner circle." },
            new() { Id = "c6", Title = "The Meeting", Description = "Intelligence confirms the coup is set for midnight." },
            new() { Id = "c7", Title = "Countdown", Description = "The team moves into position at the capital." },
            new() { Id = "c8", Title = "Communications Cut", Description = "Phone lines go dead - it's starting." },
            new() { Id = "c9", Title = "Breach", Description = "Armed units surround the government building." },
            new() { Id = "c10", Title = "Agents Act", Description = "Our team takes down the signal jammers." },
            new() { Id = "c11", Title = "President Secured", Description = "The leader is evacuated to safety." },
            new() { Id = "c12", Title = "Resistance Forms", Description = "Loyal units rally and counter-attack." },
            new() { Id = "c13", Title = "General Arrested", Description = "The rogue is caught trying to flee." },
            new() { Id = "c14", Title = "Situation Normal", Description = "Order is restored throughout the city." },
            new() { Id = "c15", Title = "Debrief", Description = "The team receives commendations for their行动." },
            new() { Id = "c16", Title = "New Dawn", Description = "The nation rests safe - democracy prevails." }
        }
    };

    public static StoryData GetDefaultStory() => _defaultStory;
}

public class StoryData
{
    public required string StoryId { get; set; }
    public required string StoryName { get; set; }
    public required string[] OrderedSolutionIds { get; set; }
    public required List<StoryCard> AllCards { get; set; }
}