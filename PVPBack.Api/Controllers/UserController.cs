using Microsoft.AspNetCore.Mvc;
using PVPBack.Core.Services;

namespace PVPBack.Controllers;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly SessionService _sessionService;

    public UserController(SessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet("{leaderId:guid}/credits")]
    public async Task<ActionResult<GetCreditsResponse>> GetCredits(
        [FromRoute] Guid leaderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var credits = await _sessionService.GetRemainingCreditsAsync(leaderId, cancellationToken);

            return Ok(new GetCreditsResponse
            {
                LeaderId = leaderId,
                RemainingCredits = credits
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = ex.Message
            });
        }
    }

    public class GetCreditsResponse
    {
        public Guid LeaderId { get; set; }
        public int RemainingCredits { get; set; }
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = null!;
    }
}