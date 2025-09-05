using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using FlashAssessment.Api;
using Xunit;

public sealed class CachingTests
{
    [Fact]
    public async Task Sanitization_Reflects_CacheInvalidation_On_Delete()
    {
        using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var unique = "CacheX-" + Guid.NewGuid().ToString("N");
        var create = await client.PostAsJsonAsync("/api/v1/words", new { word = unique, isActive = true });
        create.EnsureSuccessStatusCode();

        var request = new
        {
            text = $"{unique}",
            options = new { strategy = "FullMask", wholeWordOnly = true }
        };

        // First sanitize: should match
        var s1 = await client.PostAsJsonAsync("/api/v1/sanitize", request);
        s1.EnsureSuccessStatusCode();
        var r1 = await s1.Content.ReadFromJsonAsync<FlashAssessment.Application.Sanitization.SanitizeResponseDto>();
        r1!.Matched.Count.Should().BeGreaterThan(0);

        // Delete the word -> invalidation should occur
        var id = (await client.GetFromJsonAsync<FlashAssessment.Application.Words.Dto.SensitiveWordResponseDto>(create.Headers.Location!))!.SensitiveWordId;
        var del = await client.DeleteAsync($"/api/v1/words/{id}");
        del.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        // Second sanitize: should have no matches now (allow brief retry for cache/regex rebuild)
        FlashAssessment.Application.Sanitization.SanitizeResponseDto? r2 = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var s2 = await client.PostAsJsonAsync("/api/v1/sanitize", request);
            s2.EnsureSuccessStatusCode();
            r2 = await s2.Content.ReadFromJsonAsync<FlashAssessment.Application.Sanitization.SanitizeResponseDto>();
            if (r2!.Matched.Count == 0) break;
            await Task.Delay(100);
        }
        r2!.Matched.Should().NotContain(m => string.Equals(m.Word, unique, StringComparison.OrdinalIgnoreCase));
    }
}


