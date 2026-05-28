using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Options;
using PVPBack.Domain.Entities;
using Stripe.Checkout;

namespace PVPBack.Infrastructure.Services;

public class StripePaymentService(IAppDbContext db, IOptions<StripeSettings> settings) : IStripePaymentService
{
    private readonly StripeSettings _settings = settings.Value;

    public async Task<string> CreateCheckoutUrlAsync(
        Guid userId,
        int credits,
        CancellationToken cancellationToken = default)
    {
        if (credits is < 1 or > 400)
            throw new InvalidOperationException("Credits must be between 1 and 400.");

        var userExists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
            throw new InvalidOperationException("User not found.");

        var unitAmountCents = (long)Math.Round(
            _settings.PricePerCreditEur * 100,
            MidpointRounding.AwayFromZero);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            PaymentMethodTypes = new List<string> { "card" },
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId.ToString(),
                ["credits"] = credits.ToString(),
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = credits,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "eur",
                        UnitAmount = unitAmountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Game session credit",
                            Description = "One time session for 4 players",
                        },
                    },
                },
            ],
            SuccessUrl = _settings.SuccessUrl,
            CancelUrl = _settings.CancelUrl,
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
            throw new InvalidOperationException("Stripe did not return a checkout URL.");

        return session.Url;
    }

    public async Task<int> ConfirmAndGrantCreditsAsync(
        string stripeSessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stripeSessionId))
            throw new InvalidOperationException("Session id is required.");

        var alreadyProcessed = await db.ProcessedStripeCheckouts
            .AnyAsync(x => x.StripeSessionId == stripeSessionId, cancellationToken);

        if (alreadyProcessed)
        {
            var existing = await db.ProcessedStripeCheckouts
                .AsNoTracking()
                .FirstAsync(x => x.StripeSessionId == stripeSessionId, cancellationToken);

            var existingUser = await db.Users
                .AsNoTracking()
                .FirstAsync(u => u.Id == existing.UserId, cancellationToken);

            return existingUser.RemainingCredits;
        }

        var sessionService = new SessionService();
        var session = await sessionService.GetAsync(stripeSessionId, cancellationToken: cancellationToken);

        if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Payment not completed.");

        if (!session.Metadata.TryGetValue("userId", out var userIdRaw) ||
            !Guid.TryParse(userIdRaw, out var userId) ||
            !session.Metadata.TryGetValue("credits", out var creditsRaw) ||
            !int.TryParse(creditsRaw, out var credits))
        {
            throw new InvalidOperationException("Invalid Stripe session metadata.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            throw new InvalidOperationException("User not found.");

        user.RemainingCredits += credits;

        var pricePaid = credits * _settings.PricePerCreditEur;

        db.ProcessedStripeCheckouts.Add(new ProcessedStripeCheckout
        {
            StripeSessionId = stripeSessionId,
            UserId = userId,
            CreditsGranted = credits,
            PricePaid = pricePaid,
            ProcessedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        return user.RemainingCredits;
    }
}