namespace FlashAssessment.Application.Common;

/// <summary>
/// Options for validating incoming JSON request sizes.
/// </summary>
public sealed class RequestLimitsOptions
{
    /// <summary>
    /// Maximum allowed Content-Length for JSON requests, in bytes.
    /// </summary>
    public int MaxJsonBytes { get; init; } = 100_000;
}


