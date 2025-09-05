using FlashAssessment.Application.Sanitization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FlashAssessment.Api.Controllers;

[ApiController]
[Route("api/v1/sanitize")]
[EnableRateLimiting("sanitize")]
public sealed class SanitizationController : ControllerBase
{
    private readonly ISanitizationService _service;

    public SanitizationController(ISanitizationService service)
    {
        _service = service;
    }

    /// <summary>
    /// Sanitizes the provided text by masking sensitive words.
    /// </summary>
    /// <remarks>
    /// Returns the sanitized text, the list of matches, and elapsed processing time.
    /// Use the options to control masking strategy and case/word-boundary behavior.
    /// </remarks>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SanitizeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Sanitize([FromBody] SanitizeRequestDto dto, CancellationToken ct)
    {
        if (dto is null)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "Body is required" });
        }
        var response = await _service.SanitizeAsync(dto, ct);
        return Ok(response);
    }
}


