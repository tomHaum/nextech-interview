using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

internal sealed class HackerNewsClient : IHackerNewsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HackerNewsClient> _logger;

    public HackerNewsClient(HttpClient http, IOptions<HackerNewsOptions> options, ILogger<HackerNewsClient> logger)
    {
        _http = http;
        _logger = logger;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(options.Value.BaseUrl);
        }
    }

    public async Task<IReadOnlyList<int>> GetNewStoryIdsAsync(CancellationToken ct)
    {
        try
        {
            var ids = await _http.GetFromJsonAsync<int[]>("newstories.json", ct);
            return ids ?? Array.Empty<int>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Endpoint}", "newstories.json");
            throw;
        }
    }

    public async Task<HnItem?> GetItemAsync(int id, CancellationToken ct)
    {
        try
        {
            return await _http.GetFromJsonAsync<HnItem?>($"item/{id}.json", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Endpoint}", $"item/{id}.json");
            throw;
        }
    }
}
