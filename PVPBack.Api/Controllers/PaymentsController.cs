using Microsoft.AspNetCore.Mvc;
using PVPBack.Core.Interfaces;
using PVPBack.Domain.Dtos;

namespace PVPBack.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IStripePaymentService _stripePaymentService;

    public PaymentsController(IStripePaymentService stripePaymentService)
    {
        _stripePaymentService = stripePaymentService;
    }

    /// <summary>Before payment — returns Stripe Checkout URL.</summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<string>> CreateCheckoutSession(
        [FromBody] CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = await _stripePaymentService.CreateCheckoutUrlAsync(
                request.UserId,
                request.Credits,
                cancellationToken);
            return Ok(url);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>After redirect — verifies payment with Stripe and grants credits.</summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<ConfirmPaymentResponse>> ConfirmPayment(
        [FromBody] string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var remainingCredits = await _stripePaymentService.ConfirmAndGrantCreditsAsync(
                sessionId,
                cancellationToken);
            return Ok(new ConfirmPaymentResponse { RemainingCredits = remainingCredits });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public class ConfirmPaymentResponse
    {
        public int RemainingCredits { get; set; }
    }
}