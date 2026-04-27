using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nextech.Api.HackerNews;

namespace Nextech.Api.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Provide a fake Application Insights connection string so the Azure Monitor
        // OpenTelemetry exporter doesn't throw "connection string not found" at startup.
        // No telemetry is actually transmitted in tests.
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationInsights:ConnectionString"] =
                    "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
                    "IngestionEndpoint=https://localhost:0/;LiveEndpoint=https://localhost:0/",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the real IHackerNewsClient registration (registered via AddHttpClient)
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IHackerNewsClient));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddSingleton<IHackerNewsClient, FakeHackerNewsClient>();
        });
    }

    /// <summary>
    /// Waits until the story cache has been warmed with at least <paramref name="minCount"/> stories.
    /// Throws <see cref="TimeoutException"/> if the cache is not warm within <paramref name="timeout"/>.
    /// </summary>
    public async Task WaitForCacheWarmAsync(int minCount = 3, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        using var scope = Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IStoryCache>();

        while (true)
        {
            var current = cache.Count;
            if (current >= minCount) break;
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    $"Story cache did not reach {minCount} items within the timeout. Current count: {current}");
            await Task.Delay(50);
        }
    }
}
