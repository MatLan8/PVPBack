using Microsoft.AspNetCore.Mvc;
using PVPBack.Infrastructure.Services;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Uses [FromForm] so you can paste multi-line prompts directly into Swagger 
    /// without breaking the JSON/CURL format.
    /// </summary>
    [HttpPost("ask")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<MistralTestResponse>> AskMistral([FromForm] MistralTestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { Error = "Prompt cannot be empty." });
        }

        try
        {
            // 1. REMOVE ALL ENTERS (0x0A / 0x0D)
            // We replace them with a space so words stay separated.
            string cleanPrompt = request.Prompt
                .Replace("\r\n", " ") // Windows
                .Replace("\n", " ")   // Unix/Linux (0x0A)
                .Replace("\r", " ");  // Old Mac

            // 2. Collapse multiple spaces (caused by Enters) into a single space
            cleanPrompt = Regex.Replace(cleanPrompt, @"\s+", " ").Trim();

            // 3. Send the single-line "flattened" prompt to the AI
            var aiResponse = await _mistralService.GetAiResponseAsync(cleanPrompt);

            return Ok(new MistralTestResponse
            {
                // Returns the clean, single-line version of what was sent
                Prompt = cleanPrompt,
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