using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

public interface IStoryCache
{
    void Set(IReadOnlyList<Story> stories);
    StoriesResponse Query(string? search, int page, int pageSize);
    int Count { get; }
}
