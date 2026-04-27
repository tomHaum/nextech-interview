using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

internal interface IStoryCache
{
    void Set(IReadOnlyList<Story> stories);
    StoriesResponse Query(string? search, int page, int pageSize);
    int Count { get; }
}
