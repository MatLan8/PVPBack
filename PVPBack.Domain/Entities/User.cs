namespace PVPBack.Domain.Entities;

public class User
{
    public required Guid Id { get; set; }
    public required string Email { get; set; } = null!;
    public required string DisplayName { get; set; }
    public required string Password { get; set; }
    public required int RemainingCredits { get; set; }

    public List<GameSession> CreatedSessions { get; set; } = new();
}