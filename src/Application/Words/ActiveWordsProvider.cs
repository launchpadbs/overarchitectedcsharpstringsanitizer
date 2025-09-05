using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FlashAssessment.Application.Words;

public interface IActiveWordsProvider
{
    Task<Regex> GetRegexAsync(bool wholeWordOnly, bool caseSensitive, CancellationToken cancellationToken);
    Task InvalidateAsync();
    Task<IReadOnlyList<string>> GetActiveWordsAsync(bool caseSensitive, CancellationToken cancellationToken);
}

public sealed class ActiveWordsProvider : IActiveWordsProvider
{
    private readonly ISensitiveWordRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ActiveWordsProvider> _logger;
    private readonly Microsoft.Extensions.Options.IOptionsMonitor<ActiveWordsCacheOptions> _options;
    private readonly Microsoft.Extensions.Options.IOptionsMonitor<FlashAssessment.Application.Common.ResilienceOptions> _resilience;
    private readonly FlashAssessment.Application.Common.ISanitizationMetrics? _metrics;
    private static readonly object CacheLock = new();
    private static readonly ActivitySource Activity = new("FlashAssessment");

    private const string CacheKeyPrefix = "ActiveWordsRegex_"; // + wholeWordOnly + caseSensitive

    public ActiveWordsProvider(ISensitiveWordRepository repository, IMemoryCache cache, ILogger<ActiveWordsProvider> logger, Microsoft.Extensions.Options.IOptionsMonitor<ActiveWordsCacheOptions> options, Microsoft.Extensions.Options.IOptionsMonitor<FlashAssessment.Application.Common.ResilienceOptions> resilience, FlashAssessment.Application.Common.ISanitizationMetrics? metrics = null)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
        _options = options;
        _resilience = resilience;
        _metrics = metrics;
    }

    public async Task<Regex> GetRegexAsync(bool wholeWordOnly, bool caseSensitive, CancellationToken cancellationToken)
    {
        using var activity = Activity.StartActivity("ActiveWordsProvider.GetRegexAsync", ActivityKind.Internal);
        activity?.SetTag("cache.wholeWordOnly", wholeWordOnly);
        activity?.SetTag("cache.caseSensitive", caseSensitive);

        var baseKey = CacheKey(wholeWordOnly, caseSensitive);
        if (_cache.TryGetValue<Regex>(baseKey, out var cached))
        {
            _metrics?.CacheHit();
            activity?.SetTag("cache.hit", true);
            return cached!;
        }

        // Load all active words in a single query
        var (items, total) = await _repository.ListAsync(search: null, isActive: true, page: 1, pageSize: int.MaxValue, cancellationToken);
        activity?.SetTag("db.totalActiveWords", total);
        // Optional cap to limit alternation size in regex to protect memory; log if truncated
        // Keeping behavior conservative: do not truncate unless explicitly configured in future options.
        _logger.LogInformation("Loaded {Count} active words for regex compilation", total);

        // Build alternation pattern, escape words
        var words = items.Select(w => caseSensitive ? w.Word : w.NormalizedWord)
                         .Where(s => !string.IsNullOrWhiteSpace(s))
                         .Distinct(StringComparer.Ordinal)
                         .OrderByDescending(s => s.Length) // longest first helps regex engine a bit
                         .Select(Regex.Escape)
                         .ToArray();

        var alternation = words.Length == 0 ? "(?!x)x" : string.Join("|", words);
        // Compute a simple configuration hash (words + flags) to key compiled regex
        using var sha = System.Security.Cryptography.SHA256.Create();
        var configBytes = System.Text.Encoding.UTF8.GetBytes(string.Join("|", words) + "|" + wholeWordOnly + "|" + caseSensitive);
        var hash = Convert.ToHexString(sha.ComputeHash(configBytes));
        var key = baseKey + "_" + hash;
        if (_cache.TryGetValue<Regex>(key, out var already))
        {
            _metrics?.CacheHit();
            activity?.SetTag("cache.hitVersioned", true);
            return already!;
        }
        var boundary = wholeWordOnly ? "\\b" : string.Empty;
        var pattern = boundary + "(?:" + alternation + ")" + boundary;
        var regexOptions = caseSensitive
            ? RegexOptions.Compiled | RegexOptions.NonBacktracking
            : RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
        var timeout = TimeSpan.FromSeconds(2);
        var regex = new Regex(pattern, regexOptions, timeout);

        var ttlMinutes = _options.CurrentValue.ActiveWordsMinutes > 0 ? _options.CurrentValue.ActiveWordsMinutes : 5;
        var entryOptions = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ttlMinutes) };

        lock (CacheLock)
        {
            _cache.Set(key, regex, entryOptions);
            // also set the base key to point to latest version to avoid recomputing hash next time
            _cache.Set(baseKey, regex, entryOptions);
        }
        _metrics?.CacheMiss();
        _logger.LogInformation("Compiled regex with {WordCount} words, wholeWordOnly={WholeWord}, caseSensitive={CaseSensitive}", words.Length, wholeWordOnly, caseSensitive);
        activity?.SetTag("cache.compiledWordCount", words.Length);

        return regex;
    }

    public async Task<IReadOnlyList<string>> GetActiveWordsAsync(bool caseSensitive, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.ListAsync(search: null, isActive: true, page: 1, pageSize: int.MaxValue, cancellationToken);
        _logger.LogInformation("Loaded {Count} active words for degraded matching", total);
        var words = items.Select(w => caseSensitive ? w.Word : w.NormalizedWord)
                         .Where(s => !string.IsNullOrWhiteSpace(s))
                         .Distinct(StringComparer.Ordinal)
                         .OrderByDescending(s => s.Length)
                         .ToArray();
        return words;
    }

    public Task InvalidateAsync()
    {
        // Clear all combinations
        _cache.Remove(CacheKey(true, false));
        _cache.Remove(CacheKey(true, true));
        _cache.Remove(CacheKey(false, false));
        _cache.Remove(CacheKey(false, true));
        _metrics?.RecordCacheEviction();
        return Task.CompletedTask;
    }

    private static string CacheKey(bool wholeWordOnly, bool caseSensitive) => CacheKeyPrefix + wholeWordOnly + "_" + caseSensitive;
}


