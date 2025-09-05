using System.Diagnostics.Metrics;

namespace FlashAssessment.Application.Common;

public interface ISanitizationMetrics
{
    void RecordSanitizeDuration(double elapsedMs);
    void IncrementMatches(int count);
    void CacheHit();
    void CacheMiss();
    void RateLimitRejected();
    void RecordCacheEviction();
    void RecordError();
}


