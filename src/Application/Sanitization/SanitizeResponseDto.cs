namespace FlashAssessment.Application.Sanitization;

public sealed class SanitizeResponseDto
{
    /// <summary>
    /// The sanitized text with sensitive words masked.
    /// </summary>
    public required string SanitizedText { get; init; }
    /// <summary>
    /// Matches found in the original text (word, start offset, length).
    /// </summary>
    public required IReadOnlyList<MatchDto> Matched { get; init; }
    /// <summary>
    /// Processing time in milliseconds for visibility.
    /// </summary>
    public required double ElapsedMs { get; init; }
}

public sealed class MatchDto
{
    /// <summary>
    /// The matched sensitive word (as found in text).
    /// </summary>
    public required string Word { get; init; }
    /// <summary>
    /// Zero-based start index of the match in the original text.
    /// </summary>
    public required int Start { get; init; }
    /// <summary>
    /// Length of the match.
    /// </summary>
    public required int Length { get; init; }
}


