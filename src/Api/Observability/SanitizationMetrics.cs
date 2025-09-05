using System.Diagnostics.Metrics;
using FlashAssessment.Application.Common;

namespace FlashAssessment.Api.Observability;

public sealed class SanitizationMetrics : ISanitizationMetrics
{
    private static readonly Meter Meter = new("FlashAssessment", "1.0.0");
    private static readonly Histogram<double> DurationMs = Meter.CreateHistogram<double>("sanitize.duration.ms");
    private static readonly Counter<long> MatchCount = Meter.CreateCounter<long>("sanitize.matches");
    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("sanitize.cache.hits");
    private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("sanitize.cache.misses");
    private static readonly Counter<long> RateLimitRejects = Meter.CreateCounter<long>("sanitize.ratelimit.rejects");
    private static readonly Counter<long> CacheEvictions = Meter.CreateCounter<long>("sanitize.cache.evictions");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("sanitize.errors");
    private static readonly ObservableGauge<long> WorkingSetBytes = Meter.CreateObservableGauge("process.working_set.bytes", () => new Measurement<long>(Environment.WorkingSet));

    public void RecordSanitizeDuration(double elapsedMs) => DurationMs.Record(elapsedMs);
    public void IncrementMatches(int count) => MatchCount.Add(count);
    public void CacheHit() => CacheHits.Add(1);
    public void CacheMiss() => CacheMisses.Add(1);
    public void RateLimitRejected() => RateLimitRejects.Add(1);
    public void RecordCacheEviction() => CacheEvictions.Add(1);
    public void RecordError() => Errors.Add(1);
}


