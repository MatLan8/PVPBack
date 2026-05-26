namespace PVPBack.Domain.Dtos;

public class LeaderSessionResponseDto
{
    public Guid SessionId { get; set; } 
    public string SessionCode { get; set; } = null!; 
    public DateTime CreatedAtUtc { get; set; } 
    public DateTime? ReportCreatedAtUtc { get; set; }
    public Guid? ReportId { get; set; }
    public string? RawJson { get; set; }
}