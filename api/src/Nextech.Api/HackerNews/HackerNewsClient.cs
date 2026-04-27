using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

public sealed class HackerNewsClient : IHackerNewsClient
{
    private readonly HttpClient _http;

    public HackerNewsClient(HttpClient http, IOptions<HackerNewsOptions> options)
    {
        _http = http;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(options.Value.BaseUrl);
        }
    }

    public async Task<IReadOnlyList<int>> GetNewStoryIdsAsync(CancellationToken ct)
    {
        var ids = await _http.GetFromJsonAsync<int[]>("newstories.json", ct);
        return ids ?? Array.Empty<int>();
    }

    public async Task<HnItem?> GetItemAsync(int id, CancellationToken ct)
    {
        return await _http.GetFromJsonAsync<HnItem?>($"item/{id}.json", ct);
    }
}
