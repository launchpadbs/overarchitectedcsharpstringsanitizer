using System.Text.Json.Serialization;

namespace FlashAssessment.Application.Sanitization;

public sealed class SanitizeOptionsDto
{
    /// <summary>
    /// Mask character to use for replacement (single character string).
    /// </summary>
    [JsonPropertyName("maskChar")]
    public string? MaskCharacter { get; init; } = "*";
    /// <summary>
    /// Masking strategy: FullMask, FirstLastOnly, FixedLength, or Hash.
    /// </summary>
    public MaskStrategy Strategy { get; init; } = MaskStrategy.FullMask;
    /// <summary>
    /// If true, only mask whole words (respects Unicode boundaries).
    /// </summary>
    public bool? WholeWordOnly { get; init; }
    /// <summary>
    /// If true, matching is case sensitive.
    /// </summary>
    public bool? CaseSensitive { get; init; }
    /// <summary>
    /// If true, preserve casing for non-masked portions (FirstLastOnly only).
    /// </summary>
    public bool PreserveCasing { get; init; }
    /// <summary>
    /// Number of mask characters to use for FixedLength strategy.
    /// </summary>
    public int? FixedLength { get; init; }
}


