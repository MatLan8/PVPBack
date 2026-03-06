using Microsoft.AspNetCore.Mvc;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Services;

namespace PVPBack.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionController : ControllerBase
{
    private readonly SessionService _sessionService;
    private readonly IAiEvaluationService _aiEvaluationService;

    public SessionController(
        SessionService sessionService,
        IAiEvaluationService aiEvaluationService)
    {
        _sessionService = sessionService;
        _aiEvaluationService = aiEvaluationService;
    }

    [HttpPost("start")]
    public async Task<ActionResult<StartSessionResponse>> Start(
        [FromBody] StartSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _sessionService.StartSessionAsync(request.LeaderId, cancellationToken);

            return Ok(new StartSessionResponse
            {
                SessionId = session.Id,
                SessionCode = session.SessionCode,
                CreatedAtUtc = session.CreatedAtUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    [HttpPost("{sessionCode}/complete")]
    public async Task<ActionResult<CompleteSessionResponse>> Complete(
        [FromRoute] string sessionCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _sessionService.CompleteSessionAsync(
                sessionCode,
                _aiEvaluationService,
                cancellationToken);

            return Ok(new CompleteSessionResponse
            {
                SessionCode = sessionCode,
                Summary = summary
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    public class StartSessionRequest
    {
        public Guid LeaderId { get; set; }
    }

    public class StartSessionResponse
    {
        public Guid SessionId { get; set; }
        public string SessionCode { get; set; } = null!;
        public DateTime CreatedAtUtc { get; set; }
    }

    public class CompleteSessionResponse
    {
        public string SessionCode { get; set; } = null!;
        public string Summary { get; set; } = null!;
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = null!;
    }
}