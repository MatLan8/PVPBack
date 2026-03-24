using Microsoft.AspNetCore.Mvc;
using PVPBack.Infrastructure.Services;

namespace PVPBack.Controllers;

[ApiController]
[Route("api/mistral")]
public class MistralTestController : ControllerBase
{
    private readonly IMistralService _mistralService;

    public MistralTestController(IMistralService mistralService)
    {
        _mistralService = mistralService;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<MistralTestResponse>> AskMistral([FromBody] MistralTestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { Error = "Prompt cannot be empty." });
        }

        try
        {
            var aiResponse = await _mistralService.GetAiResponseAsync(request.Prompt);

            return Ok(new MistralTestResponse
            {
                Prompt = request.Prompt,
                Response = aiResponse
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    public class MistralTestRequest
    {
        public string Prompt { get; set; } = null!;
    }

    public class MistralTestResponse
    {
        public string Prompt { get; set; } = null!;
        public string Response { get; set; } = null!;
    }
}