using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

internal sealed class StoryRefreshService : BackgroundService
{
    private readonly IHackerNewsClient _client;
    private readonly IStoryCache _cache;
    private readonly HackerNewsOptions _options;
    private readonly ILogger<StoryRefreshService> _logger;

    public StoryRefreshService(
        IHackerNewsClient client,
        IStoryCache cache,
        IOptions<HackerNewsOptions> options,
        ILogger<StoryRefreshService> logger)
    {
        _client = client;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshOnceAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.RefreshIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { return; }

            await RefreshOnceAsync(stoppingToken);
        }
    }

    public async Task RefreshOnceAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        IReadOnlyList<int> ids;
        try
        {
            ids = await _client.GetNewStoryIdsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch newstories ID list; keeping stale cache");
            return;
        }

        var capped = ids.Take(_options.MaxStories).ToArray();
        var sem = new SemaphoreSlim(_options.ItemFetchConcurrency);
        var tasks = capped.Select(id => FetchOne(id, sem, ct)).ToArray();
        var results = await Task.WhenAll(tasks);

        var stories = results
            .Where(item => item is not null)
            .Where(item => item!.Type == "story" && !item.Dead && !item.Deleted && item.Title is not null)
            .Select(item => new Story(item!.Id, item.Title!, item.Url, item.By ?? "", item.Time, item.Score))
            .ToList();

        _cache.Set(stories);
        sw.Stop();
        _logger.LogInformation("Cache refreshed: {Count} stories in {ElapsedMs}ms", stories.Count, sw.ElapsedMilliseconds);
    }

    private async Task<HnItem?> FetchOne(int id, SemaphoreSlim sem, CancellationToken ct)
    {
        await sem.WaitAsync(ct);
        try { return await _client.GetItemAsync(id, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch HN item {Id}", id);
            return null;
        }
        finally { sem.Release(); }
    }
}
