using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

internal sealed class StoryCache : IStoryCache
{
    private IReadOnlyList<Story> _stories = Array.Empty<Story>();

    public int Count => Volatile.Read(ref _stories).Count;

    public void Set(IReadOnlyList<Story> stories)
    {
        Volatile.Write(ref _stories, stories);
    }

    public StoriesResponse Query(string? search, int page, int pageSize)
    {
        var snapshot = Volatile.Read(ref _stories);

        IEnumerable<Story> filtered = snapshot;
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = snapshot.Where(s =>
                s.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var matched = filtered.ToList();
        var skip = (page - 1) * pageSize;
        var items = skip >= matched.Count
            ? Array.Empty<Story>()
            : matched.Skip(skip).Take(pageSize).ToArray();

        return new StoriesResponse(items, matched.Count, page, pageSize);
    }
}
