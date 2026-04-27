using System.Net;
using System.Text.Json;
using FluentAssertions;
using Nextech.Api.Models;

namespace Nextech.Api.IntegrationTests;

public sealed class StoriesEndpointTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StoriesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.WaitForCacheWarmAsync(minCount: 3);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private async Task<StoriesResponse> GetStoriesResponseAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<StoriesResponse>(json, JsonOptions)!;
    }

    [Fact]
    public async Task GetStories_Default_ReturnsThreeStories()
    {
        var result = await GetStoriesResponseAsync("/api/stories");

        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetStories_SearchAlpha_ReturnsAlphaStory()
    {
        var result = await GetStoriesResponseAsync("/api/stories?search=alpha");

        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Alpha Story");
    }

    [Fact]
    public async Task GetStories_Page1PageSize2_ReturnsTwoItemsWithTotal3()
    {
        var result = await GetStoriesResponseAsync("/api/stories?page=1&pageSize=2");

        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetStories_BetaStory_HasNullUrl()
    {
        var result = await GetStoriesResponseAsync("/api/stories");

        var beta = result.Items.SingleOrDefault(s => s.Title == "Beta Story");
        beta.Should().NotBeNull();
        beta!.Url.Should().BeNull();
    }

    [Fact]
    public async Task GetStories_PageZero_Returns400()
    {
        var response = await _client.GetAsync("/api/stories?page=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStories_PageSizeOver100_Returns400()
    {
        var response = await _client.GetAsync("/api/stories?pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
