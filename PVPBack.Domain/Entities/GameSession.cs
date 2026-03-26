namespace PVPBack.Domain.Entities;

public class GameSession
{
    public required Guid Id { get; set; }
    public required string SessionCode { get; set; }

    public required Guid LeaderId { get; set; }
    public User Leader { get; set; } = null!;

    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public AiEvaluationResult? AiEvaluationResult { get; set; }
}