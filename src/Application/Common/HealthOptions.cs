namespace FlashAssessment.Application.Common;

/// <summary>
/// Health check configuration values.
/// </summary>
public sealed class HealthOptions
{
    /// <summary>
    /// SQL health check command timeout in seconds.
    /// </summary>
    public int SqlTimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// Regex cache health check timeout in milliseconds.
    /// </summary>
    public int RegexCheckMs { get; init; } = 500;
}


