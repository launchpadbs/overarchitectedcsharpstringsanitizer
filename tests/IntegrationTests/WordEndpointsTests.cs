// Integration tests spin up the WebApplicationFactory and exercise HTTP endpoints
// end-to-end (controllers + validators + repositories + DB) to verify behavior
// and status codes.
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using FlashAssessment.Api;
using System.Net;

public class WordEndpointsTests
{
    [Fact]
    public async Task CreateAndGet_Word_Succeeds()
    {
        // Arrange an in-memory test server
        using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();
        // Create a unique word to avoid conflicts
        var unique = "Example-" + Guid.NewGuid().ToString("N");
        var create = new { word = unique, category = "Test", severity = 2, isActive = true };
        // Act: POST then GET the created resource
        var post = await client.PostAsJsonAsync("/api/v1/words", create);
        post.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        // Get Location header id
        var location = post.Headers.Location;
        location.Should().NotBeNull();
        var get = await client.GetAsync(location);
        get.EnsureSuccessStatusCode();
        var body = await get.Content.ReadFromJsonAsync<FlashAssessment.Application.Words.Dto.SensitiveWordResponseDto>();
        body!.Word.Should().Be(unique);
    }

    [Fact]
    public async Task Sanitize_ReturnsMaskedText_ForCreatedWord()
    {
        // Arrange: create a word then sanitize text that contains it
        using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        var unique = "Secret" + Guid.NewGuid().ToString("N");
        var create = new { word = unique, category = "SanitizeTest", severity = 1, isActive = true };
        var post = await client.PostAsJsonAsync("/api/v1/words", create);
        post.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        var request = new
        {
            text = $"This contains {unique} in it",
            options = new { strategy = "FullMask", wholeWordOnly = true }
        };

        // Act: sanitize text containing the sensitive word
        var sanitize = await client.PostAsJsonAsync("/api/v1/sanitize", request);
        sanitize.EnsureSuccessStatusCode();
        var payload = await sanitize.Content.ReadFromJsonAsync<FlashAssessment.Application.Sanitization.SanitizeResponseDto>();
        payload!.SanitizedText.Should().NotContain(unique);
        payload.Matched.Should().NotBeNull();
        payload.Matched.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task List_Paginates_And_Searches()
    {
        // Arrange: create multiple items, then request a paged search
        using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        // create a couple
        var uniq1 = "Paginate-" + Guid.NewGuid().ToString("N");
        var uniq2 = "Paginate-" + Guid.NewGuid().ToString("N");
        await client.PostAsJsonAsync("/api/v1/words", new { word = uniq1, category = "P", isActive = true });
        await client.PostAsJsonAsync("/api/v1/words", new { word = uniq2, category = "P", isActive = true });

        // Act: page=1&pageSize=1 should still return X-Total-Count > 1
        var list = await client.GetAsync($"/api/v1/words?search=Paginate-&page=1&pageSize=1");
        list.EnsureSuccessStatusCode();
        list.Headers.TryGetValues("X-Total-Count", out var totals).Should().BeTrue();
        int.Parse(totals!.First()).Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        // Arrange test server only; use an unlikely id
        using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();
        var resp = await client.GetAsync("/api/v1/words/999999999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ConcurrencyConflict_Returns409()
    {
        // Arrange: create a word, then update with a wrong rowversion
        using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();

        // create
        var uniq = "Concurrency-" + Guid.NewGuid().ToString("N");
        var post = await client.PostAsJsonAsync("/api/v1/words", new { word = uniq, isActive = true });
        post.EnsureSuccessStatusCode();
        var location = post.Headers.Location!;
        var get = await client.GetAsync(location);
        var dto = await get.Content.ReadFromJsonAsync<FlashAssessment.Application.Words.Dto.SensitiveWordResponseDto>();

        // Act: PUT with wrong rowVersion should cause a 409
        var wrongRv = new byte[] { 9, 9, 9 };
        var put = await client.PutAsJsonAsync($"/api/v1/words/{dto!.SensitiveWordId}", new {
            word = uniq,
            isActive = true,
            category = (string?)null,
            severity = (int?)null,
            rowVersion = wrongRv
        });

        put.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}


