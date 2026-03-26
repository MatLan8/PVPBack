using Microsoft.AspNetCore.Mvc;
using PVPBack.Core.Services;

namespace PVPBack.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionController : ControllerBase
{
    private readonly SessionService _sessionService;

    public SessionController(SessionService sessionService)
    {
        _sessionService = sessionService;
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
            var summary = await _sessionService.CompleteSessionAsync(sessionCode, cancellationToken);

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

    [HttpGet("{sessionCode}/report")]
    public async Task<ActionResult<GetSessionReportResponse>> GetReport(
        [FromRoute] string sessionCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _sessionService.GetSessionReportAsync(sessionCode, cancellationToken);

            return Ok(new GetSessionReportResponse
            {
                SessionCode = report.SessionCode,
                Summary = report.Summary,
                Report = report.Report,
                CreatedAtUtc = report.CreatedAtUtc
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

    public class GetSessionReportResponse
    {
        public string SessionCode { get; set; } = null!;
        public string Summary { get; set; } = null!;
        public object Report { get; set; } = null!;
        public DateTime CreatedAtUtc { get; set; }
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = null!;
    }
}