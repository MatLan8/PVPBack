using System.ComponentModel.DataAnnotations;

namespace PVPBack.Domain.Entities;

public class ProcessedStripeCheckout
{
    [Key] public required string StripeSessionId { get; set; }
    public required Guid UserId { get; set; }
    public int CreditsGranted { get; set; }
    public decimal PricePaid { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}