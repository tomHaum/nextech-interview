using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

internal interface IHackerNewsClient
{
    Task<IReadOnlyList<int>> GetNewStoryIdsAsync(CancellationToken ct);
    Task<HnItem?> GetItemAsync(int id, CancellationToken ct);
}
