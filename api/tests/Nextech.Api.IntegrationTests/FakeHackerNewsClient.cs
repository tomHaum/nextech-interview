using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.IntegrationTests;

internal sealed class FakeHackerNewsClient : IHackerNewsClient
{
    public Task<IReadOnlyList<int>> GetNewStoryIdsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<int>>(new[] { 1, 2, 3, 4, 5 });

    public Task<HnItem?> GetItemAsync(int id, CancellationToken ct)
    {
        HnItem? item = id switch
        {
            1 => new HnItem { Id = 1, Title = "Alpha Story", Url = "https://alpha.com", By = "user1", Time = 1700000001, Score = 100, Type = "story" },
            2 => new HnItem { Id = 2, Title = "Beta Story",  Url = null,                By = "user2", Time = 1700000002, Score = 200, Type = "story" },
            3 => new HnItem { Id = 3, Title = "Gamma Story", Url = "https://gamma.com", By = "user3", Time = 1700000003, Score = 300, Type = "story" },
            4 => new HnItem { Id = 4, Title = "A Comment",   Url = null,                By = "user4", Time = 1700000004, Score = 0,   Type = "comment" },  // filtered by RefreshService (not a "story")
            _ => null,                                                                                                                                              // id == 5: null => also filtered out
        };
        return Task.FromResult(item);
    }
}
