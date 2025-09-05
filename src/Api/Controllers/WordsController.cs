using FlashAssessment.Application.Words;
using FlashAssessment.Application.Words.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FlashAssessment.Api.Controllers;

[ApiController]
[Route("api/v1/words")]
[EnableRateLimiting("words_read")]
public sealed class WordsController : ControllerBase
{
    private readonly ISensitiveWordService _service;

    public WordsController(ISensitiveWordService service)
    {
        _service = service;
    }

    /// <summary>
    /// Creates a new sensitive word.
    /// </summary>
    /// <returns>201 Created with a Location header.</returns>
    [HttpPost]
    [EnableRateLimiting("words_write")]
    [ProducesResponseType(typeof(void), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create([FromBody] CreateSensitiveWordRequestDto dto, CancellationToken ct)
    {
        var id = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id }, null);
    }

    /// <summary>
    /// Gets a sensitive word by its id.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SensitiveWordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] long id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Lists sensitive words with optional search and paging.
    /// </summary>
    /// <remarks>
    /// Returns items and sets X-Total-Count header with the total number of records.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SensitiveWordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> List([FromQuery] ListWordsQueryDto query, CancellationToken ct)
    {
        var result = await _service.ListAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result.Items);
    }

    /// <summary>
    /// Updates an existing sensitive word.
    /// </summary>
    /// <remarks>
    /// Uses optimistic concurrency via RowVersion. Returns 204 on success.
    /// </remarks>
    [HttpPut("{id:long}")]
    [EnableRateLimiting("words_write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] UpdateSensitiveWordRequestDto dto, CancellationToken ct)
    {
        await _service.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes a sensitive word by id.
    /// </summary>
    [HttpDelete("{id:long}")]
    [EnableRateLimiting("words_write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Delete([FromRoute] long id, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}


