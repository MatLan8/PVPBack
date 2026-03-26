namespace PVPBack.Domain.Entities;

public class AiEvaluationResult
{
    public required Guid Id { get; set; }

    public required Guid GameSessionId { get; set; }
    public GameSession GameSession { get; set; } = null!;

    public required string Summary { get; set; }
    public required string RawJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}