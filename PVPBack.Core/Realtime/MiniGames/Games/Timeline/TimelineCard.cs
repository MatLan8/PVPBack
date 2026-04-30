namespace PVPBack.Core.Realtime.MiniGames.Games.Timeline;

/// <summary>
/// Represents a single timeline card with an ID, title, and description.
/// </summary>
public class TimelineCard
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    public TimelineCard(string id, string title, string description)
    {
        Id = id;
        Title = title;
        Description = description;
    }
}

/// <summary>
/// Represents a story template with a theme and collection of cards.
/// </summary>
public class StoryTemplate
{
    public string Theme { get; set; } = string.Empty;
    public List<TimelineCard> Cards { get; set; } = new();
}