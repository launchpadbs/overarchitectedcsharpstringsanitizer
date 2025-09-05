using System;

namespace FlashAssessment.Application.Common.RateLimiting
{
    /// <summary>
    /// Options to configure fixed-window rate limits for API endpoints.
    /// </summary>
    public sealed class RateLimitingOptions
    {
        public FixedWindowPolicyOptions Sanitize { get; init; } = new();
        public FixedWindowPolicyOptions WordsRead { get; init; } = new();
        public FixedWindowPolicyOptions WordsWrite { get; init; } = new();
    }

    /// <summary>
    /// Fixed window policy configuration.
    /// </summary>
    public sealed class FixedWindowPolicyOptions
    {
        public int PermitLimit { get; init; } = 60;
        public int WindowSeconds { get; init; } = 60;
        public int QueueLimit { get; init; } = 0;
        public bool AutoReplenishment { get; init; } = true;
    }
}


