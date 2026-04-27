using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.UnitTests;

public class StoryRefreshServiceTests
{
    private static HnItem Item(int id, string? title = "t", string? type = "story", bool dead = false, bool deleted = false) =>
        new() { Id = id, Title = title, Type = type, Dead = dead, Deleted = deleted, By = "u", Url = $"https://x/{id}" };

    private static StoryRefreshService CreateSut(IHackerNewsClient client, IStoryCache cache, int maxStories = 500)
    {
        var opts = Options.Create(new HackerNewsOptions
        {
            MaxStories = maxStories,
            RefreshIntervalSeconds = 60,
            ItemFetchConcurrency = 4
        });
        return new StoryRefreshService(client, cache, opts, NullLogger<StoryRefreshService>.Instance);
    }

    [Fact]
    public async Task RefreshOnceAsync_loads_items_and_writes_to_cache()
    {
        var client = new Mock<IHackerNewsClient>();
        client.Setup(c => c.GetNewStoryIdsAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { 1, 2, 3 });
        client.Setup(c => c.GetItemAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Item(1));
        client.Setup(c => c.GetItemAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(Item(2));
        client.Setup(c => c.GetItemAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(Item(3));
        var cache = new StoryCache();
        var sut = CreateSut(client.Object, cache);

        await sut.RefreshOnceAsync(CancellationToken.None);

        cache.Count.Should().Be(3);
    }

    [Fact]
    public async Task RefreshOnceAsync_respects_MaxStories_cap()
    {
        var client = new Mock<IHackerNewsClient>();
        client.Setup(c => c.GetNewStoryIdsAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(Enumerable.Range(1, 10).ToArray());
        client.Setup(c => c.GetItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((int id, CancellationToken _) => Item(id));
        var cache = new StoryCache();
        var sut = CreateSut(client.Object, cache, maxStories: 3);

        await sut.RefreshOnceAsync(CancellationToken.None);

        cache.Count.Should().Be(3);
        client.Verify(c => c.GetItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task RefreshOnceAsync_filters_null_dead_deleted_and_non_story_items()
    {
        var client = new Mock<IHackerNewsClient>();
        client.Setup(c => c.GetNewStoryIdsAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { 1, 2, 3, 4, 5 });
        client.Setup(c => c.GetItemAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Item(1));
        client.Setup(c => c.GetItemAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((HnItem?)null);
        client.Setup(c => c.GetItemAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(Item(3, dead: true));
        client.Setup(c => c.GetItemAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(Item(4, type: "comment"));
        client.Setup(c => c.GetItemAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(Item(5, deleted: true));
        var cache = new StoryCache();
        var sut = CreateSut(client.Object, cache);

        await sut.RefreshOnceAsync(CancellationToken.None);

        cache.Count.Should().Be(1);
    }

    [Fact]
    public async Task RefreshOnceAsync_keeps_existing_cache_when_GetNewStoryIds_throws()
    {
        var cache = new StoryCache();
        cache.Set(new[] { new Story(99, "stale", null, "u", 0, 0) });
        var client = new Mock<IHackerNewsClient>();
        client.Setup(c => c.GetNewStoryIdsAsync(It.IsAny<CancellationToken>()))
              .ThrowsAsync(new HttpRequestException("boom"));
        var sut = CreateSut(client.Object, cache);

        await sut.RefreshOnceAsync(CancellationToken.None);

        cache.Count.Should().Be(1);
    }

    [Fact]
    public async Task RefreshOnceAsync_skips_items_that_throw_but_keeps_others()
    {
        var client = new Mock<IHackerNewsClient>();
        client.Setup(c => c.GetNewStoryIdsAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { 1, 2, 3 });
        client.Setup(c => c.GetItemAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Item(1));
        client.Setup(c => c.GetItemAsync(2, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("transient"));
        client.Setup(c => c.GetItemAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(Item(3));
        var cache = new StoryCache();
        var sut = CreateSut(client.Object, cache);

        await sut.RefreshOnceAsync(CancellationToken.None);

        cache.Count.Should().Be(2);
    }
}
