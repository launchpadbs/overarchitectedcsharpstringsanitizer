namespace FlashAssessment.Application.Sanitization;

public sealed class SanitizeRequestDto
{
    /// <summary>
    /// The input text that will be sanitized.
    /// </summary>
    public required string Text { get; init; }
    /// <summary>
    /// Optional sanitization options to control masking behavior.
    /// </summary>
    public SanitizeOptionsDto? Options { get; init; }
}


