using System.Text.RegularExpressions;
using FlashAssessment.Application.Words;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlashAssessment.Api.Health;

public sealed class RegexCacheHealthCheck : IHealthCheck
{
    private readonly IMemoryCache _cache;
    private readonly IActiveWordsProvider _provider;
    private readonly FlashAssessment.Application.Common.HealthOptions _options;

    public RegexCacheHealthCheck(IMemoryCache cache, IActiveWordsProvider provider, Microsoft.Extensions.Options.IOptions<FlashAssessment.Application.Common.HealthOptions> options)
    {
        _cache = cache;
        _provider = provider;
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Attempt to build or fetch a default regex quickly; if it throws or takes too long, mark unhealthy
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(_options.RegexCheckMs));
            var regex = _provider.GetRegexAsync(wholeWordOnly: true, caseSensitive: false, cts.Token).GetAwaiter().GetResult();
            return Task.FromResult(HealthCheckResult.Healthy("Regex cache available"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Regex cache/provider failed", ex));
        }
    }
}


