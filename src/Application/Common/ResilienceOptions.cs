namespace FlashAssessment.Application.Common;

/// <summary>
/// Options controlling database resilience policies (retry and circuit breaker).
/// </summary>
public sealed class ResilienceOptions
{
    public int ReadRetryCount { get; init; } = 2;
    public int WriteRetryCount { get; init; } = 3;
    public int BaseDelayMs { get; init; } = 100;
    public int CircuitFailures { get; init; } = 5;
    public int BreakSeconds { get; init; } = 30;
    public int SlowQueryMs { get; init; } = 500;
    public int RegexTimeoutMs { get; init; } = 2000;
}


