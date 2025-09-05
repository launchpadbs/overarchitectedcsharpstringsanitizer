// Unit tests for the in-process sanitizer engine. These verify matching
// semantics and masking strategies independent of HTTP or database.
using System.Threading;
using System.Threading.Tasks;
using FlashAssessment.Application.Sanitization;
using FlashAssessment.Application.Words;
using FlashAssessment.Domain.Words;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Microsoft.Extensions.Options;

public class SanitizationServiceTests
{
    // Ensures whole-word, case-insensitive matching masks expected tokens
    // and returns both masked segments in the response.
    [Fact]
    public async Task FullMask_WholeWord_MasksMatches()
    {
        var repo = Substitute.For<ISensitiveWordRepository>();
        repo.ListAsync(null, true, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => (new[]
            {
                new SensitiveWord { SensitiveWordId = 1, Word = "create", NormalizedWord = "create", IsActive = true, CreatedUtc = System.DateTime.UtcNow, RowVersion = new byte[]{1} },
                new SensitiveWord { SensitiveWordId = 2, Word = "string", NormalizedWord = "string", IsActive = true, CreatedUtc = System.DateTime.UtcNow, RowVersion = new byte[]{1} }
            } as IReadOnlyList<SensitiveWord>, 2));
        var provider = new ActiveWordsProvider(
            repo,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ActiveWordsProvider>>(),
            Options.Create(new ActiveWordsCacheOptions()).ToOptionsMonitor(),
            Options.Create(new FlashAssessment.Application.Common.ResilienceOptions()).ToOptionsMonitor());
        var service = new SanitizationService(provider);
        var req = new SanitizeRequestDto
        {
            Text = "You need to create a string",
            Options = new SanitizeOptionsDto { Strategy = MaskStrategy.FullMask, WholeWordOnly = true }
        };

        var res = await service.SanitizeAsync(req);
        res.SanitizedText.Should().Be("You need to ****** a ******");
        res.Matched.Should().HaveCount(2);
    }

    // Verifies that case-insensitive matching is the default behavior
    // (both uppercase and lowercase variants should be masked).
    [Fact]
    public async Task CaseSensitive_Off_ByDefault_MatchesRegardlessOfCase()
    {
        var repo = Substitute.For<ISensitiveWordRepository>();
        repo.ListAsync(null, true, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new[]
            {
                new SensitiveWord { SensitiveWordId = 1, Word = "Create", NormalizedWord = "create", IsActive = true, CreatedUtc = System.DateTime.UtcNow, RowVersion = new byte[]{1} }
            } as IReadOnlyList<SensitiveWord>, 1));
        var provider = new ActiveWordsProvider(
            repo,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ActiveWordsProvider>>(),
            Options.Create(new ActiveWordsCacheOptions()).ToOptionsMonitor(),
            Options.Create(new FlashAssessment.Application.Common.ResilienceOptions()).ToOptionsMonitor());
        var service = new SanitizationService(provider);
        var req = new SanitizeRequestDto { Text = "CREATE and create", Options = new SanitizeOptionsDto { WholeWordOnly = true } };
        var res = await service.SanitizeAsync(req);
        res.Matched.Count.Should().Be(2);
    }

    // Confirms the FixedLength strategy replaces the matched token
    // with the exact number of mask characters specified.
    [Fact]
    public async Task FixedLength_ReplacesWithFixedCount()
    {
        var repo = Substitute.For<ISensitiveWordRepository>();
        repo.ListAsync(null, true, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new[]
            {
                new SensitiveWord { SensitiveWordId = 1, Word = "secret", NormalizedWord = "secret", IsActive = true, CreatedUtc = System.DateTime.UtcNow, RowVersion = new byte[]{1} }
            } as IReadOnlyList<SensitiveWord>, 1));
        var provider = new ActiveWordsProvider(
            repo,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ActiveWordsProvider>>(),
            Options.Create(new ActiveWordsCacheOptions()).ToOptionsMonitor(),
            Options.Create(new FlashAssessment.Application.Common.ResilienceOptions()).ToOptionsMonitor());
        var service = new SanitizationService(provider);
        var req = new SanitizeRequestDto { Text = "a secret here", Options = new SanitizeOptionsDto { Strategy = MaskStrategy.FixedLength, FixedLength = 3, WholeWordOnly = true } };
        var res = await service.SanitizeAsync(req);
        res.SanitizedText.Should().Contain("a *** here");
    }

    // Ensures the Hash strategy masks the token with a short 8-char
    // SHA-256 hex, removing the original sensitive token from output.
    [Fact]
    public async Task HashStrategy_ProducesShortHash()
    {
        var repo = Substitute.For<ISensitiveWordRepository>();
        repo.ListAsync(null, true, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new[]
            {
                new SensitiveWord { SensitiveWordId = 1, Word = "token", NormalizedWord = "token", IsActive = true, CreatedUtc = System.DateTime.UtcNow, RowVersion = new byte[]{1} }
            } as IReadOnlyList<SensitiveWord>, 1));
        var provider = new ActiveWordsProvider(
            repo,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ActiveWordsProvider>>(),
            Options.Create(new ActiveWordsCacheOptions()).ToOptionsMonitor(),
            Options.Create(new FlashAssessment.Application.Common.ResilienceOptions()).ToOptionsMonitor());
        var service = new SanitizationService(provider);
        var req = new SanitizeRequestDto { Text = "token present", Options = new SanitizeOptionsDto { Strategy = MaskStrategy.Hash, WholeWordOnly = true } };
        var res = await service.SanitizeAsync(req);
        res.SanitizedText.Should().NotContain("token");
        // Expect 8 hex chars replacing 'token'
        res.SanitizedText.Split(' ')[0].Length.Should().Be(8);
    }
}


