namespace PVPBack.Core.Options;

public class StripeSettings
{
    public string SecretKey { get; set; } = null!;
    public string SuccessUrl { get; set; } = null!;
    public string CancelUrl { get; set; } = null!;
    public decimal PricePerCreditEur { get; set; } = 14.99m;
}