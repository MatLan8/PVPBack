namespace PVPBack.Core.Interfaces;

public interface IStripePaymentService
{
    Task<string> CreateCheckoutUrlAsync(Guid userId, int credits, CancellationToken cancellationToken = default);
    Task<int> ConfirmAndGrantCreditsAsync(string stripeSessionId, CancellationToken cancellationToken = default);
}