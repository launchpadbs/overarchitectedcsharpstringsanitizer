using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using FlashAssessment.Api;
using Xunit;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task Sanitize_Throttles_When_OverLimit()
    {
        using var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("RateLimiting:Sanitize:PermitLimit", "2");
                builder.UseSetting("RateLimiting:Sanitize:WindowSeconds", "60");
            });

        var client = app.CreateClient();

        var payload = new
        {
            text = "a b c",
            options = new { strategy = "FullMask", wholeWordOnly = true }
        };

        // 1st and 2nd should succeed
        (await client.PostAsJsonAsync("/api/v1/sanitize", payload)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/sanitize", payload)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 3rd within same window should be 429
        var third = await client.PostAsJsonAsync("/api/v1/sanitize", payload);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}


