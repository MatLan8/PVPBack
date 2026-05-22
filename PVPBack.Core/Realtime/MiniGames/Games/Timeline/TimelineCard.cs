namespace PVPBack.Core.Realtime.MiniGames.Games.Timeline;

/// <summary>
/// Represents a single timeline card with an ID and local image reference.
/// </summary>
public class TimelineCard
{
    public string Id { get; set; }
    public string ImageName { get; set; }

    public TimelineCard(string id, string imageName)
    {
        Id = id;
        ImageName = imageName;
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