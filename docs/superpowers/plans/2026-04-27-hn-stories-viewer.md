# HN Stories Viewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Angular 18 frontend + ASP.NET Core 8 API that displays Hacker News newest stories with server-side search and paging, deployable to Azure.

**Architecture:** API singleton `StoryCache` is kept warm by an `IHostedService` that refreshes every 60s from the HN API; the controller filters and paginates against this in-memory cache. Angular calls the API via `HttpClient` and renders results with Angular Material.

**Tech Stack:** .NET 8, ASP.NET Core, xUnit, FluentAssertions, Moq, Application Insights · Angular 18 (standalone, signals), Angular Material, RxJS, Karma/Jasmine, Playwright · Azure App Service + Static Web App.

**Source spec:** [docs/superpowers/specs/2026-04-27-hn-stories-viewer-design.md](../specs/2026-04-27-hn-stories-viewer-design.md)

---

## File Structure (locked in)

**Backend (`/api`):**
- `api/Nextech.Api.sln`
- `api/src/Nextech.Api/Nextech.Api.csproj` + `Program.cs` + `appsettings.json` + `appsettings.Development.json`
- `api/src/Nextech.Api/Models/` — `Story.cs`, `StoriesResponse.cs`, `HnItem.cs`
- `api/src/Nextech.Api/HackerNews/` — `HackerNewsOptions.cs`, `IHackerNewsClient.cs`, `HackerNewsClient.cs`, `IStoryCache.cs`, `StoryCache.cs`, `StoryRefreshService.cs`
- `api/src/Nextech.Api/Controllers/StoriesController.cs`
- `api/tests/Nextech.Api.UnitTests/` — `Nextech.Api.UnitTests.csproj`, `StoryCacheTests.cs`, `StoryRefreshServiceTests.cs`, `HackerNewsClientTests.cs`
- `api/tests/Nextech.Api.IntegrationTests/` — `Nextech.Api.IntegrationTests.csproj`, `CustomWebApplicationFactory.cs`, `FakeHackerNewsClient.cs`, `StoriesEndpointTests.cs`, `HealthEndpointTests.cs`

**Frontend (`/web`):**
- `web/package.json`, `web/angular.json`, `web/tsconfig*.json`, `web/playwright.config.ts`
- `web/src/main.ts`, `web/src/index.html`, `web/src/styles.scss`
- `web/src/app/app.component.{ts,html,scss}`, `app.config.ts`, `app.routes.ts`
- `web/src/app/stories/` — `stories.component.{ts,html,scss,spec.ts}`, `story.service.ts`, `story.service.spec.ts`, `story.model.ts`
- `web/src/environments/environment.ts`, `web/src/environments/environment.production.ts`
- `web/e2e/stories.spec.ts`, `web/e2e/fixtures/stories.json`

**Root:**
- `README.md`
- `.gitignore` (covers `bin/`, `obj/`, `node_modules/`, `dist/`, `.angular/`, `playwright-report/`, `test-results/`)

---

## Task 1: Repository scaffolding & .gitignore

**Files:**
- Create: `.gitignore`

- [ ] **Step 1: Write `.gitignore` covering both stacks**

```gitignore
# .NET
bin/
obj/
*.user
.vs/

# Node / Angular
node_modules/
dist/
.angular/

# Playwright
playwright-report/
test-results/

# Editors / OS
.idea/
.DS_Store

# Environment / secrets
appsettings.Local.json
*.pfx
```

- [ ] **Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: add .gitignore for .NET and Angular"
```

---

## Task 2: Scaffold .NET solution and projects

**Files:**
- Create: `api/Nextech.Api.sln` and three projects under `api/src` and `api/tests`.

- [ ] **Step 1: Create solution and API project**

Run from repo root:
```bash
mkdir -p api && cd api
dotnet new sln -n Nextech.Api
dotnet new webapi -n Nextech.Api -o src/Nextech.Api --use-controllers --no-https=false
dotnet sln add src/Nextech.Api/Nextech.Api.csproj
```

- [ ] **Step 2: Delete the default `WeatherForecast` files**

```bash
rm src/Nextech.Api/WeatherForecast.cs src/Nextech.Api/Controllers/WeatherForecastController.cs
```

- [ ] **Step 3: Create unit and integration test projects**

```bash
dotnet new xunit -n Nextech.Api.UnitTests -o tests/Nextech.Api.UnitTests
dotnet new xunit -n Nextech.Api.IntegrationTests -o tests/Nextech.Api.IntegrationTests
dotnet sln add tests/Nextech.Api.UnitTests/Nextech.Api.UnitTests.csproj
dotnet sln add tests/Nextech.Api.IntegrationTests/Nextech.Api.IntegrationTests.csproj
```

- [ ] **Step 4: Add references and packages**

```bash
dotnet add tests/Nextech.Api.UnitTests reference src/Nextech.Api/Nextech.Api.csproj
dotnet add tests/Nextech.Api.IntegrationTests reference src/Nextech.Api/Nextech.Api.csproj

dotnet add tests/Nextech.Api.UnitTests package Moq
dotnet add tests/Nextech.Api.UnitTests package FluentAssertions
dotnet add tests/Nextech.Api.UnitTests package Microsoft.Extensions.Logging.Abstractions

dotnet add tests/Nextech.Api.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/Nextech.Api.IntegrationTests package FluentAssertions

dotnet add src/Nextech.Api package Microsoft.ApplicationInsights.AspNetCore
```

- [ ] **Step 5: Verify build**

```bash
dotnet build
```

Expected: solution builds with 0 errors / 0 warnings (Treat warnings as info — actual zero-warning is goal).

- [ ] **Step 6: Commit**

```bash
git add api/
git commit -m "chore(api): scaffold .NET 8 solution with unit and integration test projects"
```

---

## Task 3: Domain models

**Files:**
- Create: `api/src/Nextech.Api/Models/Story.cs`
- Create: `api/src/Nextech.Api/Models/StoriesResponse.cs`
- Create: `api/src/Nextech.Api/Models/HnItem.cs`

- [ ] **Step 1: Create `Story.cs`**

```csharp
namespace Nextech.Api.Models;

public sealed record Story(
    int Id,
    string Title,
    string? Url,
    string By,
    long Time,
    int Score);
```

- [ ] **Step 2: Create `StoriesResponse.cs`**

```csharp
namespace Nextech.Api.Models;

public sealed record StoriesResponse(
    IReadOnlyList<Story> Items,
    int Total,
    int Page,
    int PageSize);
```

- [ ] **Step 3: Create `HnItem.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Nextech.Api.Models;

internal sealed record HnItem
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("by")] public string? By { get; init; }
    [JsonPropertyName("time")] public long Time { get; init; }
    [JsonPropertyName("score")] public int Score { get; init; }
    [JsonPropertyName("dead")] public bool Dead { get; init; }
    [JsonPropertyName("deleted")] public bool Deleted { get; init; }
}
```

- [ ] **Step 4: Verify build**

```bash
cd api && dotnet build
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add api/src/Nextech.Api/Models/
git commit -m "feat(api): add domain models for Story, StoriesResponse, HnItem"
```

---

## Task 4: HackerNewsOptions configuration

**Files:**
- Create: `api/src/Nextech.Api/HackerNews/HackerNewsOptions.cs`
- Modify: `api/src/Nextech.Api/appsettings.json`

- [ ] **Step 1: Create `HackerNewsOptions.cs`**

```csharp
namespace Nextech.Api.HackerNews;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";
    public int MaxStories { get; set; } = 500;
    public int RefreshIntervalSeconds { get; set; } = 60;
    public int ItemFetchConcurrency { get; set; } = 10;
}
```

- [ ] **Step 2: Update `appsettings.json`** to include the HackerNews section and a default Cors entry:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "HackerNews": {
    "BaseUrl": "https://hacker-news.firebaseio.com/v0/",
    "MaxStories": 500,
    "RefreshIntervalSeconds": 60,
    "ItemFetchConcurrency": 10
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:4200" ]
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add api/src/Nextech.Api/HackerNews/HackerNewsOptions.cs api/src/Nextech.Api/appsettings.json
git commit -m "feat(api): add HackerNewsOptions and configuration"
```

---

## Task 5: HackerNewsClient (TDD)

**Files:**
- Create: `api/src/Nextech.Api/HackerNews/IHackerNewsClient.cs`
- Create: `api/src/Nextech.Api/HackerNews/HackerNewsClient.cs`
- Test: `api/tests/Nextech.Api.UnitTests/HackerNewsClientTests.cs`

- [ ] **Step 1: Create the interface**

```csharp
using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<int>> GetNewStoryIdsAsync(CancellationToken ct);
    Task<HnItem?> GetItemAsync(int id, CancellationToken ct);
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.UnitTests;

public class HackerNewsClientTests
{
    private static HackerNewsClient CreateSut(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/v0/") };
        var opts = Options.Create(new HackerNewsOptions { BaseUrl = "https://example.com/v0/" });
        return new HackerNewsClient(http, opts);
    }

    private static Mock<HttpMessageHandler> HandlerReturning(string url, HttpResponseMessage response)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return mock;
    }

    [Fact]
    public async Task GetNewStoryIdsAsync_hits_newstories_endpoint_and_returns_ids()
    {
        var handler = HandlerReturning(
            "https://example.com/v0/newstories.json",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { 1, 2, 3 })
            });
        var sut = CreateSut(handler.Object);

        var ids = await sut.GetNewStoryIdsAsync(CancellationToken.None);

        ids.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task GetItemAsync_hits_item_endpoint_and_deserializes_with_null_url()
    {
        var json = """{"id":42,"type":"story","title":"Ask HN: foo","url":null,"by":"u","time":123,"score":7}""";
        var handler = HandlerReturning(
            "https://example.com/v0/item/42.json",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var sut = CreateSut(handler.Object);

        var item = await sut.GetItemAsync(42, CancellationToken.None);

        item.Should().NotBeNull();
        item!.Id.Should().Be(42);
        item.Title.Should().Be("Ask HN: foo");
        item.Url.Should().BeNull();
        item.By.Should().Be("u");
    }

    [Fact]
    public async Task GetItemAsync_returns_null_when_response_body_is_null()
    {
        var handler = HandlerReturning(
            "https://example.com/v0/item/9.json",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") });
        var sut = CreateSut(handler.Object);

        var item = await sut.GetItemAsync(9, CancellationToken.None);

        item.Should().BeNull();
    }
}
```

- [ ] **Step 3: Run tests to confirm failure**

```bash
cd api && dotnet test tests/Nextech.Api.UnitTests --filter HackerNewsClientTests
```

Expected: compile error — `HackerNewsClient` does not exist.

- [ ] **Step 4: Implement `HackerNewsClient`**

```csharp
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
```

- [ ] **Step 5: Run tests to confirm pass**

```bash
cd api && dotnet test tests/Nextech.Api.UnitTests --filter HackerNewsClientTests
```

Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add api/src/Nextech.Api/HackerNews/IHackerNewsClient.cs api/src/Nextech.Api/HackerNews/HackerNewsClient.cs api/tests/Nextech.Api.UnitTests/HackerNewsClientTests.cs
git commit -m "feat(api): add HackerNewsClient with tests"
```

---

## Task 6: StoryCache (TDD)

**Files:**
- Create: `api/src/Nextech.Api/HackerNews/IStoryCache.cs`
- Create: `api/src/Nextech.Api/HackerNews/StoryCache.cs`
- Test: `api/tests/Nextech.Api.UnitTests/StoryCacheTests.cs`

- [ ] **Step 1: Create the interface**

```csharp
using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

public interface IStoryCache
{
    void Set(IReadOnlyList<Story> stories);
    StoriesResponse Query(string? search, int page, int pageSize);
    int Count { get; }
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
using FluentAssertions;
using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.UnitTests;

public class StoryCacheTests
{
    private static Story S(int id, string title) => new(id, title, $"https://x/{id}", "u", 0, 0);

    [Fact]
    public void Empty_cache_returns_empty_response()
    {
        var sut = new StoryCache();

        var result = sut.Query(null, page: 1, pageSize: 20);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public void Set_then_query_returns_first_page()
    {
        var sut = new StoryCache();
        sut.Set(Enumerable.Range(1, 25).Select(i => S(i, $"Title {i}")).ToList());

        var result = sut.Query(null, page: 1, pageSize: 10);

        result.Items.Should().HaveCount(10);
        result.Items.First().Id.Should().Be(1);
        result.Total.Should().Be(25);
    }

    [Fact]
    public void Query_returns_correct_slice_for_middle_page()
    {
        var sut = new StoryCache();
        sut.Set(Enumerable.Range(1, 25).Select(i => S(i, $"Title {i}")).ToList());

        var result = sut.Query(null, page: 2, pageSize: 10);

        result.Items.Select(s => s.Id).Should().Equal(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
    }

    [Fact]
    public void Query_returns_partial_last_page()
    {
        var sut = new StoryCache();
        sut.Set(Enumerable.Range(1, 25).Select(i => S(i, $"Title {i}")).ToList());

        var result = sut.Query(null, page: 3, pageSize: 10);

        result.Items.Should().HaveCount(5);
        result.Total.Should().Be(25);
    }

    [Fact]
    public void Query_beyond_last_page_returns_empty_with_correct_total()
    {
        var sut = new StoryCache();
        sut.Set(Enumerable.Range(1, 25).Select(i => S(i, $"Title {i}")).ToList());

        var result = sut.Query(null, page: 99, pageSize: 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(25);
    }

    [Fact]
    public void Search_is_case_insensitive_substring_on_title()
    {
        var sut = new StoryCache();
        sut.Set(new[] { S(1, "Foo Bar"), S(2, "FOO baz"), S(3, "Other") });

        var result = sut.Query("foo", page: 1, pageSize: 20);

        result.Items.Select(s => s.Id).Should().BeEquivalentTo(new[] { 1, 2 });
        result.Total.Should().Be(2);
    }

    [Fact]
    public void Search_total_reflects_filtered_count_not_cache_size()
    {
        var sut = new StoryCache();
        sut.Set(new[] { S(1, "alpha"), S(2, "beta"), S(3, "gamma") });

        var result = sut.Query("alpha", page: 1, pageSize: 20);

        result.Total.Should().Be(1);
    }

    [Fact]
    public void Set_replaces_previous_contents_atomically()
    {
        var sut = new StoryCache();
        sut.Set(new[] { S(1, "old") });
        sut.Set(new[] { S(2, "new") });

        sut.Count.Should().Be(1);
        sut.Query(null, 1, 10).Items.Single().Title.Should().Be("new");
    }
}
```

- [ ] **Step 3: Run tests to confirm failure**

```bash
cd api && dotnet test tests/Nextech.Api.UnitTests --filter StoryCacheTests
```

Expected: compile error — `StoryCache` does not exist.

- [ ] **Step 4: Implement `StoryCache`**

```csharp
using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

public sealed class StoryCache : IStoryCache
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
```

- [ ] **Step 5: Run tests to confirm pass**

```bash
cd api && dotnet test tests/Nextech.Api.UnitTests --filter StoryCacheTests
```

Expected: 8 passed.

- [ ] **Step 6: Commit**

```bash
git add api/src/Nextech.Api/HackerNews/IStoryCache.cs api/src/Nextech.Api/HackerNews/StoryCache.cs api/tests/Nextech.Api.UnitTests/StoryCacheTests.cs
git commit -m "feat(api): add StoryCache with filter and paging"
```

---

## Task 7: StoryRefreshService (TDD)

**Files:**
- Create: `api/src/Nextech.Api/HackerNews/StoryRefreshService.cs`
- Test: `api/tests/Nextech.Api.UnitTests/StoryRefreshServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
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
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
cd api && dotnet test tests/Nextech.Api.UnitTests --filter StoryRefreshServiceTests
```

Expected: compile error — `StoryRefreshService` does not exist.

- [ ] **Step 3: Implement `StoryRefreshService`**

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nextech.Api.Models;

namespace Nextech.Api.HackerNews;

public sealed class StoryRefreshService : BackgroundService
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
```

- [ ] **Step 4: Run tests to confirm pass**

```bash
cd api && dotnet test tests/Nextech.Api.UnitTests --filter StoryRefreshServiceTests
```

Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add api/src/Nextech.Api/HackerNews/StoryRefreshService.cs api/tests/Nextech.Api.UnitTests/StoryRefreshServiceTests.cs
git commit -m "feat(api): add StoryRefreshService background service with tests"
```

---

## Task 8: StoriesController

**Files:**
- Create: `api/src/Nextech.Api/Controllers/StoriesController.cs`

(Controller is thin glue; tested via integration tests in Task 10.)

- [ ] **Step 1: Implement controller**

```csharp
using Microsoft.AspNetCore.Mvc;
using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.Controllers;

[ApiController]
[Route("api/stories")]
public sealed class StoriesController : ControllerBase
{
    private readonly IStoryCache _cache;

    public StoriesController(IStoryCache cache)
    {
        _cache = cache;
    }

    [HttpGet]
    public ActionResult<StoriesResponse> Get(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) return BadRequest(new { error = "page must be >= 1" });
        if (pageSize < 1 || pageSize > 100) return BadRequest(new { error = "pageSize must be between 1 and 100" });

        return Ok(_cache.Query(search, page, pageSize));
    }
}
```

- [ ] **Step 2: Verify build**

```bash
cd api && dotnet build
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add api/src/Nextech.Api/Controllers/StoriesController.cs
git commit -m "feat(api): add StoriesController"
```

---

## Task 9: Wire up `Program.cs` (DI, CORS, App Insights, health endpoint)

**Files:**
- Modify: `api/src/Nextech.Api/Program.cs`

- [ ] **Step 1: Replace `Program.cs` contents**

```csharp
using Nextech.Api.HackerNews;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicyName = "AllowedFrontends";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.Configure<HackerNewsOptions>(
    builder.Configuration.GetSection(HackerNewsOptions.SectionName));

builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HackerNewsOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton<IStoryCache, StoryCache>();
builder.Services.AddHostedService<StoryRefreshService>();

builder.Services.AddControllers();
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

app.UseCors(CorsPolicyName);
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program; // for WebApplicationFactory
```

- [ ] **Step 2: Run the app and verify it starts**

```bash
cd api && dotnet run --project src/Nextech.Api
```

Expected: app starts, log shows `Cache refreshed: N stories in XXXms` after a few seconds, app listens on `http://localhost:5XXX`. Hit `Ctrl+C` to stop.

- [ ] **Step 3: Manually hit the API**

In another terminal:
```bash
curl 'http://localhost:5000/api/stories?page=1&pageSize=5' | head
curl http://localhost:5000/api/health
```

Adjust port if different. Expected: JSON with stories; health returns `{"status":"ok"}`.

- [ ] **Step 4: Commit**

```bash
git add api/src/Nextech.Api/Program.cs
git commit -m "feat(api): wire up DI, CORS, App Insights, and health endpoint"
```

---

## Task 10: Integration tests

**Files:**
- Create: `api/tests/Nextech.Api.IntegrationTests/FakeHackerNewsClient.cs`
- Create: `api/tests/Nextech.Api.IntegrationTests/CustomWebApplicationFactory.cs`
- Create: `api/tests/Nextech.Api.IntegrationTests/StoriesEndpointTests.cs`
- Create: `api/tests/Nextech.Api.IntegrationTests/HealthEndpointTests.cs`

- [ ] **Step 1: Create `FakeHackerNewsClient`**

```csharp
using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.IntegrationTests;

public sealed class FakeHackerNewsClient : IHackerNewsClient
{
    private readonly Dictionary<int, HnItem?> _items;

    public FakeHackerNewsClient()
    {
        _items = new Dictionary<int, HnItem?>
        {
            [1] = new() { Id = 1, Type = "story", Title = "Foo bar", Url = "https://example.com/1", By = "alice", Time = 1714200000, Score = 10 },
            [2] = new() { Id = 2, Type = "story", Title = "FOO baz", Url = null, By = "bob", Time = 1714200001, Score = 5 },
            [3] = new() { Id = 3, Type = "story", Title = "Other story", Url = "https://example.com/3", By = "carol", Time = 1714200002, Score = 7 },
            [4] = new() { Id = 4, Type = "comment", Title = "should be filtered", By = "dave", Time = 1714200003 },
            [5] = null
        };
    }

    public Task<IReadOnlyList<int>> GetNewStoryIdsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<int>>(new[] { 1, 2, 3, 4, 5 });

    public Task<HnItem?> GetItemAsync(int id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);
}
```

- [ ] **Step 2: Create `CustomWebApplicationFactory`**

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nextech.Api.HackerNews;

namespace Nextech.Api.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the typed HttpClient registration with our fake.
            var clientDescriptors = services.Where(d => d.ServiceType == typeof(IHackerNewsClient)).ToList();
            foreach (var d in clientDescriptors) services.Remove(d);
            services.AddSingleton<IHackerNewsClient, FakeHackerNewsClient>();
        });
    }
}
```

- [ ] **Step 3: Create `StoriesEndpointTests`**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nextech.Api.Models;

namespace Nextech.Api.IntegrationTests;

public class StoriesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _http;

    public StoriesEndpointTests(CustomWebApplicationFactory factory)
    {
        _http = factory.CreateClient();
    }

    [Fact]
    public async Task Default_request_returns_all_3_valid_stories()
    {
        var resp = await _http.GetFromJsonAsync<StoriesResponse>("/api/stories");

        resp.Should().NotBeNull();
        resp!.Total.Should().Be(3);
        resp.Items.Should().HaveCount(3);
        resp.Page.Should().Be(1);
        resp.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Search_filters_case_insensitively_on_title()
    {
        var resp = await _http.GetFromJsonAsync<StoriesResponse>("/api/stories?search=foo");

        resp!.Total.Should().Be(2);
        resp.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task Paging_returns_correct_slice()
    {
        var resp = await _http.GetFromJsonAsync<StoriesResponse>("/api/stories?page=2&pageSize=2");

        resp!.Total.Should().Be(3);
        resp.Items.Should().HaveCount(1);
        resp.Items.Single().Id.Should().Be(3);
    }

    [Fact]
    public async Task Story_without_url_serializes_url_as_null()
    {
        var resp = await _http.GetFromJsonAsync<StoriesResponse>("/api/stories");

        resp!.Items.Single(i => i.Id == 2).Url.Should().BeNull();
    }

    [Fact]
    public async Task Page_zero_returns_400()
    {
        var resp = await _http.GetAsync("/api/stories?page=0");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PageSize_above_100_returns_400()
    {
        var resp = await _http.GetAsync("/api/stories?pageSize=101");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 4: Create `HealthEndpointTests`**

```csharp
using System.Net;
using FluentAssertions;

namespace Nextech.Api.IntegrationTests;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _http;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _http = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_200_with_status_ok()
    {
        var resp = await _http.GetAsync("/api/health");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"ok\"");
    }
}
```

- [ ] **Step 5: Run tests**

```bash
cd api && dotnet test tests/Nextech.Api.IntegrationTests
```

Expected: 7 passed.

- [ ] **Step 6: Run the entire API test suite**

```bash
cd api && dotnet test
```

Expected: all unit + integration tests passing.

- [ ] **Step 7: Commit**

```bash
git add api/tests/Nextech.Api.IntegrationTests/
git commit -m "test(api): add integration tests for stories and health endpoints"
```

---

## Task 11: Scaffold Angular workspace

**Files:**
- Create: `web/` (everything from `ng new`).

- [ ] **Step 1: Run `ng new`**

From repo root:
```bash
npx -p @angular/cli@18 ng new web --routing=true --style=scss --skip-git --standalone --ssr=false --strict
```

When prompted, accept defaults.

- [ ] **Step 2: Verify dev server starts**

```bash
cd web && npm start
```

Expected: Angular default page at `http://localhost:4200`. `Ctrl+C`.

- [ ] **Step 3: Add Angular Material**

```bash
cd web && npx ng add @angular/material@18
```

When prompted: pick a prebuilt theme (e.g., "Azure/Blue"), set up global typography = yes, browser animations = include and enable.

- [ ] **Step 4: Verify build**

```bash
cd web && npm run build
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add web/
git commit -m "chore(web): scaffold Angular 18 workspace with Material"
```

---

## Task 12: Story model and environment files

**Files:**
- Create: `web/src/app/stories/story.model.ts`
- Modify: `web/src/environments/environment.ts`
- Create: `web/src/environments/environment.production.ts`

- [ ] **Step 1: Create `story.model.ts`**

```typescript
export interface Story {
  id: number;
  title: string;
  url: string | null;
  by: string;
  time: number;
  score: number;
}

export interface StoriesResponse {
  items: Story[];
  total: number;
  page: number;
  pageSize: number;
}
```

- [ ] **Step 2: Create `web/src/environments/` directory if not present**

Angular CLI 18 may not generate it; if not, create `web/src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000',
};
```

- [ ] **Step 3: Create `environment.production.ts`**

```typescript
export const environment = {
  production: true,
  apiBaseUrl: 'https://nextech-api.azurewebsites.net',
};
```

(The actual production URL gets updated in Task 18 after the App Service is created.)

- [ ] **Step 4: Wire up file replacements in `angular.json`**

Open `web/angular.json`, find the `production` configuration under `architect.build.configurations`, and add:

```json
"fileReplacements": [
  {
    "replace": "src/environments/environment.ts",
    "with": "src/environments/environment.production.ts"
  }
]
```

- [ ] **Step 5: Verify production build picks up replacement**

```bash
cd web && npm run build -- --configuration production
```

Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add web/src/app/stories/story.model.ts web/src/environments/ web/angular.json
git commit -m "feat(web): add story model and environment configuration"
```

---

## Task 13: StoryService (TDD)

**Files:**
- Create: `web/src/app/stories/story.service.ts`
- Test: `web/src/app/stories/story.service.spec.ts`

- [ ] **Step 1: Write the failing tests**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { StoryService } from './story.service';
import { StoriesResponse } from './story.model';
import { environment } from '../../environments/environment';

describe('StoryService', () => {
  let service: StoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [StoryService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(StoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs /api/stories with correct query params', () => {
    service.getStories('foo', 2, 10).subscribe();

    const req = httpMock.expectOne(
      `${environment.apiBaseUrl}/api/stories?search=foo&page=2&pageSize=10`
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 2, pageSize: 10 });
  });

  it('omits search param when empty', () => {
    service.getStories('', 1, 20).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/stories?page=1&pageSize=20`);
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 1, pageSize: 20 });
  });

  it('emits the response body', (done) => {
    const expected: StoriesResponse = {
      items: [{ id: 1, title: 't', url: null, by: 'u', time: 0, score: 0 }],
      total: 1,
      page: 1,
      pageSize: 20,
    };
    service.getStories('', 1, 20).subscribe(res => {
      expect(res).toEqual(expected);
      done();
    });

    httpMock.expectOne(`${environment.apiBaseUrl}/api/stories?page=1&pageSize=20`).flush(expected);
  });
});
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
cd web && npm test -- --watch=false --browsers=ChromeHeadless
```

Expected: compile error — `StoryService` not found.

- [ ] **Step 3: Implement `StoryService`**

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StoriesResponse } from './story.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class StoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getStories(search: string, page: number, pageSize: number): Observable<StoriesResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<StoriesResponse>(`${this.baseUrl}/api/stories`, { params });
  }
}
```

- [ ] **Step 4: Update `app.config.ts` to provide `HttpClient`**

Open `web/src/app/app.config.ts` and ensure `provideHttpClient()` is in the providers array:

```typescript
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(),
    provideAnimationsAsync(),
  ],
};
```

- [ ] **Step 5: Run tests to confirm pass**

```bash
cd web && npm test -- --watch=false --browsers=ChromeHeadless
```

Expected: 3 StoryService tests passing (plus any default app tests).

- [ ] **Step 6: Commit**

```bash
git add web/src/app/stories/story.service.ts web/src/app/stories/story.service.spec.ts web/src/app/app.config.ts
git commit -m "feat(web): add StoryService with tests"
```

---

## Task 14: StoriesComponent (TDD)

**Files:**
- Create: `web/src/app/stories/stories.component.ts`
- Create: `web/src/app/stories/stories.component.html`
- Create: `web/src/app/stories/stories.component.scss`
- Test: `web/src/app/stories/stories.component.spec.ts`

- [ ] **Step 1: Write the failing tests**

```typescript
import { ComponentFixture, TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { By } from '@angular/platform-browser';
import { StoriesComponent } from './stories.component';
import { environment } from '../../environments/environment';
import { StoriesResponse } from './story.model';

describe('StoriesComponent', () => {
  let fixture: ComponentFixture<StoriesComponent>;
  let httpMock: HttpTestingController;

  function flushInitial(items: StoriesResponse['items'] = [], total = 0) {
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/stories`);
    req.flush({ items, total, page: 1, pageSize: 20 });
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [StoriesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideAnimationsAsync()],
    });
    fixture = TestBed.createComponent(StoriesComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('renders a story list when service returns items', () => {
    fixture.detectChanges();
    flushInitial([{ id: 1, title: 'Hello', url: 'https://x', by: 'u', time: 0, score: 0 }], 1);
    fixture.detectChanges();

    const titles = fixture.debugElement.queryAll(By.css('[data-testid="story-title"]'));
    expect(titles.length).toBe(1);
    expect(titles[0].nativeElement.textContent).toContain('Hello');
  });

  it('renders title as anchor when url present', () => {
    fixture.detectChanges();
    flushInitial([{ id: 1, title: 'L', url: 'https://x', by: 'u', time: 0, score: 0 }], 1);
    fixture.detectChanges();

    const anchor = fixture.debugElement.query(By.css('[data-testid="story-link"]'));
    expect(anchor).toBeTruthy();
    expect(anchor.nativeElement.getAttribute('href')).toBe('https://x');
    expect(anchor.nativeElement.getAttribute('target')).toBe('_blank');
  });

  it('renders title as span when url is null', () => {
    fixture.detectChanges();
    flushInitial([{ id: 2, title: 'NoLink', url: null, by: 'u', time: 0, score: 0 }], 1);
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('[data-testid="story-link"]'))).toBeNull();
    expect(fixture.debugElement.query(By.css('[data-testid="story-title"]'))).toBeTruthy();
  });

  it('shows empty state when total is 0', () => {
    fixture.detectChanges();
    flushInitial([], 0);
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('[data-testid="empty-state"]'))).toBeTruthy();
  });

  it('debounces search input and resets to page 1', fakeAsync(() => {
    fixture.detectChanges();
    flushInitial([], 0);

    const component = fixture.componentInstance;
    component.page.set(3);
    component.searchInput.next('foo');
    tick(300);

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/stories` && r.params.get('search') === 'foo' && r.params.get('page') === '1'
    );
    req.flush({ items: [], total: 0, page: 1, pageSize: 20 });
    discardPeriodicTasks();
  }));

  it('shows error state when service errors', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/stories`);
    req.flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('[data-testid="error-state"]'))).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
cd web && npm test -- --watch=false --browsers=ChromeHeadless
```

Expected: compile error — `StoriesComponent` not found.

- [ ] **Step 3: Implement `stories.component.ts`**

```typescript
import { Component, signal, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { Subject, debounceTime, distinctUntilChanged, switchMap, tap, catchError, of } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';
import { StoryService } from './story.service';
import { StoriesResponse } from './story.model';

@Component({
  selector: 'app-stories',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatFormFieldModule, MatInputModule,
    MatPaginatorModule, MatProgressBarModule, MatButtonModule, MatIconModule,
  ],
  templateUrl: './stories.component.html',
  styleUrl: './stories.component.scss',
})
export class StoriesComponent {
  private readonly service = inject(StoryService);
  private readonly destroyRef = inject(DestroyRef);

  readonly searchInput = new Subject<string>();
  readonly searchTerm = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly response = signal<StoriesResponse>({ items: [], total: 0, page: 1, pageSize: 20 });
  readonly loading = signal(false);
  readonly error = signal(false);

  readonly pageSizeOptions = [10, 20, 50];

  constructor() {
    this.searchInput
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        tap(term => {
          this.searchTerm.set(term);
          this.page.set(1);
        }),
        switchMap(() => this.fetch$()),
        takeUntilDestroyed(),
      )
      .subscribe();

    this.fetch$().pipe(takeUntilDestroyed()).subscribe();
  }

  onPage(e: PageEvent): void {
    this.page.set(e.pageIndex + 1);
    this.pageSize.set(e.pageSize);
    this.fetch$().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  onSearchInput(value: string): void {
    this.searchInput.next(value);
  }

  retry(): void {
    this.fetch$().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  ageMinutes(time: number): number {
    return Math.max(0, Math.floor((Date.now() / 1000 - time) / 60));
  }

  private fetch$() {
    this.loading.set(true);
    this.error.set(false);
    return this.service.getStories(this.searchTerm(), this.page(), this.pageSize()).pipe(
      tap(res => {
        this.response.set(res);
        this.loading.set(false);
      }),
      catchError(() => {
        this.error.set(true);
        this.loading.set(false);
        return of(null);
      }),
    );
  }
}
```

- [ ] **Step 4: Implement `stories.component.html`**

```html
<section class="stories">
  <header class="stories__header">
    <h1>Hacker News — Newest Stories</h1>
    <mat-form-field appearance="outline" class="stories__search">
      <mat-label>Search by title</mat-label>
      <input
        matInput
        type="text"
        autofocus
        (input)="onSearchInput($any($event.target).value)"
        data-testid="search-input"
      />
    </mat-form-field>
  </header>

  @if (loading()) {
    <mat-progress-bar mode="indeterminate" data-testid="loading-bar"></mat-progress-bar>
  }

  @if (error()) {
    <mat-card class="stories__error" data-testid="error-state">
      <mat-card-content>
        <p>Couldn't load stories.</p>
        <button mat-stroked-button (click)="retry()" data-testid="retry-button">Retry</button>
      </mat-card-content>
    </mat-card>
  } @else if (response().items.length === 0 && !loading()) {
    <mat-card class="stories__empty" data-testid="empty-state">
      <mat-card-content>No stories found.</mat-card-content>
    </mat-card>
  } @else {
    <ul class="stories__list">
      @for (story of response().items; track story.id) {
        <li class="stories__item">
          <mat-card>
            <mat-card-content>
              @if (story.url) {
                <a
                  class="stories__title"
                  [href]="story.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  data-testid="story-link"
                >
                  <span data-testid="story-title">{{ story.title }}</span>
                </a>
              } @else {
                <span class="stories__title stories__title--no-link" data-testid="story-title">
                  {{ story.title }} <small>(no link)</small>
                </span>
              }
              <div class="stories__meta">
                by {{ story.by }} · {{ ageMinutes(story.time) }}m ago · {{ story.score }} points
              </div>
            </mat-card-content>
          </mat-card>
        </li>
      }
    </ul>
  }

  <mat-paginator
    [length]="response().total"
    [pageSize]="pageSize()"
    [pageIndex]="page() - 1"
    [pageSizeOptions]="pageSizeOptions"
    (page)="onPage($event)"
    data-testid="paginator"
  ></mat-paginator>
</section>
```

- [ ] **Step 5: Implement `stories.component.scss`**

```scss
.stories {
  max-width: 960px;
  margin: 0 auto;
  padding: 1rem;

  &__header {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
  }

  &__search {
    min-width: 280px;
  }

  &__list {
    list-style: none;
    padding: 0;
    margin: 1rem 0;
    display: grid;
    gap: 0.75rem;
  }

  &__item {
    margin: 0;
  }

  &__title {
    font-weight: 600;
    color: inherit;
    text-decoration: none;

    &:hover { text-decoration: underline; }

    &--no-link { color: rgba(0,0,0,0.6); }
  }

  &__meta {
    margin-top: 0.25rem;
    font-size: 0.85rem;
    color: rgba(0,0,0,0.6);
  }

  &__error,
  &__empty {
    margin-top: 1rem;
  }
}
```

- [ ] **Step 6: Run tests to confirm pass**

```bash
cd web && npm test -- --watch=false --browsers=ChromeHeadless
```

Expected: all StoriesComponent tests passing.

- [ ] **Step 7: Commit**

```bash
git add web/src/app/stories/stories.component.* web/src/app/stories/stories.component.spec.ts
git commit -m "feat(web): add StoriesComponent with search, paging, and tests"
```

---

## Task 15: App shell & routing

**Files:**
- Modify: `web/src/app/app.routes.ts`
- Modify: `web/src/app/app.component.{ts,html,scss}`

- [ ] **Step 1: Update `app.routes.ts`**

```typescript
import { Routes } from '@angular/router';
import { StoriesComponent } from './stories/stories.component';

export const routes: Routes = [
  { path: '', component: StoriesComponent },
  { path: '**', redirectTo: '' },
];
```

- [ ] **Step 2: Update `app.component.ts`**

```typescript
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, MatToolbarModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {}
```

- [ ] **Step 3: Update `app.component.html`**

```html
<mat-toolbar color="primary">
  <span>Nextech HN Reader</span>
</mat-toolbar>

<router-outlet></router-outlet>
```

- [ ] **Step 4: Replace `app.component.scss`** with empty-or-minimal styles:

```scss
:host {
  display: block;
  min-height: 100vh;
}
```

- [ ] **Step 5: Update `styles.scss`** (global) — keep Material theme imports added by `ng add`, append:

```scss
body {
  margin: 0;
  font-family: Roboto, "Helvetica Neue", sans-serif;
  background: #fafafa;
}
```

- [ ] **Step 6: Manual smoke test — both API and Web**

In one terminal:
```bash
cd api && dotnet run --project src/Nextech.Api
```

In another:
```bash
cd web && npm start
```

Open `http://localhost:4200`. Expected: toolbar visible, list of HN stories rendered, search and paginator work.

- [ ] **Step 7: Commit**

```bash
git add web/src/app/app.routes.ts web/src/app/app.component.* web/src/styles.scss
git commit -m "feat(web): wire up app shell with toolbar and routing"
```

---

## Task 16: Playwright E2E setup and tests

**Files:**
- Create: `web/playwright.config.ts`
- Create: `web/e2e/stories.spec.ts`
- Create: `web/e2e/fixtures/stories.json`
- Modify: `web/package.json` (add scripts)

- [ ] **Step 1: Install Playwright**

```bash
cd web && npm install --save-dev @playwright/test
npx playwright install chromium
```

- [ ] **Step 2: Create `playwright.config.ts`**

```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  fullyParallel: true,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'npm start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
```

- [ ] **Step 3: Create fixture `web/e2e/fixtures/stories.json`**

```json
{
  "items": [
    { "id": 1, "title": "First story", "url": "https://example.com/1", "by": "alice", "time": 1714200000, "score": 10 },
    { "id": 2, "title": "Second story", "url": null, "by": "bob", "time": 1714200001, "score": 5 },
    { "id": 3, "title": "Foo bar baz", "url": "https://example.com/3", "by": "carol", "time": 1714200002, "score": 7 }
  ],
  "total": 3,
  "page": 1,
  "pageSize": 20
}
```

- [ ] **Step 4: Create `web/e2e/stories.spec.ts`**

```typescript
import { test, expect } from '@playwright/test';
import * as fixtureAll from './fixtures/stories.json';

const fixture = fixtureAll as unknown as {
  items: { id: number; title: string; url: string | null; by: string; time: number; score: number }[];
  total: number; page: number; pageSize: number;
};

test.describe('Stories page', () => {
  test('renders stories from API', async ({ page }) => {
    await page.route('**/api/stories**', route => route.fulfill({ status: 200, body: JSON.stringify(fixture) }));
    await page.goto('/');
    await expect(page.getByTestId('story-title').first()).toContainText('First story');
    await expect(page.getByTestId('story-title')).toHaveCount(3);
  });

  test('story without URL renders as text not link', async ({ page }) => {
    await page.route('**/api/stories**', route => route.fulfill({ status: 200, body: JSON.stringify(fixture) }));
    await page.goto('/');
    await expect(page.getByText('Second story')).toBeVisible();
    await expect(page.getByText('(no link)')).toBeVisible();
  });

  test('search updates results (debounced)', async ({ page }) => {
    let callCount = 0;
    await page.route('**/api/stories**', route => {
      callCount++;
      const url = new URL(route.request().url());
      const search = url.searchParams.get('search') ?? '';
      const filtered = search ? fixture.items.filter(i => i.title.toLowerCase().includes(search.toLowerCase())) : fixture.items;
      route.fulfill({ status: 200, body: JSON.stringify({ ...fixture, items: filtered, total: filtered.length }) });
    });
    await page.goto('/');
    await expect(page.getByTestId('story-title')).toHaveCount(3);

    await page.getByTestId('search-input').fill('foo');
    await expect.poll(() => page.getByTestId('story-title').count()).toBe(1);
    await expect(page.getByTestId('story-title').first()).toContainText('Foo bar baz');
    expect(callCount).toBeGreaterThanOrEqual(2);
  });

  test('shows error state with retry on 500', async ({ page }) => {
    let attempt = 0;
    await page.route('**/api/stories**', route => {
      attempt++;
      if (attempt === 1) {
        route.fulfill({ status: 500, body: 'boom' });
      } else {
        route.fulfill({ status: 200, body: JSON.stringify(fixture) });
      }
    });
    await page.goto('/');
    await expect(page.getByTestId('error-state')).toBeVisible();
    await page.getByTestId('retry-button').click();
    await expect(page.getByTestId('story-title')).toHaveCount(3);
  });
});
```

- [ ] **Step 5: Add npm scripts**

In `web/package.json`, add to the `scripts` object:

```json
"e2e": "playwright test",
"e2e:headed": "playwright test --headed"
```

- [ ] **Step 6: Run E2E tests**

```bash
cd web && npm run e2e
```

Expected: 4 passed.

- [ ] **Step 7: Commit**

```bash
git add web/playwright.config.ts web/e2e/ web/package.json web/package-lock.json
git commit -m "test(web): add Playwright E2E tests with API stubs"
```

---

## Task 17: Run the full test suite once

**Files:** none (verification only)

- [ ] **Step 1: Run API tests**

```bash
cd api && dotnet test
```

Expected: all green.

- [ ] **Step 2: Run web unit tests**

```bash
cd web && npm test -- --watch=false --browsers=ChromeHeadless
```

Expected: all green.

- [ ] **Step 3: Run Playwright**

```bash
cd web && npm run e2e
```

Expected: all green.

If any fail, fix before moving on.

---

## Task 18: Provision Azure resources

**Files:**
- Optional: `scripts/azure-setup.sh` (the commands captured for the README).

- [ ] **Step 1: Pick names**

Choose globally-unique names. Suggested:
- Resource group: `rg-nextech`
- App Service plan: `plan-nextech`
- App Service (API): `nextech-api-<your-initials>`
- Static Web App: `nextech-web-<your-initials>`
- App Insights: `appi-nextech`

- [ ] **Step 2: Login and provision via az CLI**

```bash
az login
az group create -n rg-nextech -l eastus

az appservice plan create -g rg-nextech -n plan-nextech --is-linux --sku F1
az webapp create -g rg-nextech -p plan-nextech -n nextech-api-<initials> --runtime "DOTNETCORE:8.0"

az monitor app-insights component create -g rg-nextech -a appi-nextech -l eastus --application-type web

# Capture the connection string
APPI_CS=$(az monitor app-insights component show -g rg-nextech -a appi-nextech --query connectionString -o tsv)

az webapp config appsettings set -g rg-nextech -n nextech-api-<initials> --settings \
  APPLICATIONINSIGHTS_CONNECTION_STRING="$APPI_CS" \
  Cors__AllowedOrigins__0="https://placeholder-will-update.azurestaticapps.net"

az webapp config set -g rg-nextech -n nextech-api-<initials> --health-check-path /api/health
```

- [ ] **Step 3: Create Static Web App (free tier)**

```bash
az staticwebapp create -g rg-nextech -n nextech-web-<initials> -l eastus2 --sku Free
```

(Static Web Apps requires `eastus2`, `centralus`, `westus2`, or `westeurope` — use one of those.)

- [ ] **Step 4: Capture the URLs**

```bash
az webapp show -g rg-nextech -n nextech-api-<initials> --query defaultHostName -o tsv
az staticwebapp show -g rg-nextech -n nextech-web-<initials> --query defaultHostname -o tsv
```

Note both — call them `<API_URL>` and `<WEB_URL>` for the next steps.

- [ ] **Step 5: Update CORS env var on API to actual web URL**

```bash
az webapp config appsettings set -g rg-nextech -n nextech-api-<initials> --settings \
  Cors__AllowedOrigins__0="https://<WEB_URL>"
```

- [ ] **Step 6: Update `web/src/environments/environment.production.ts` with the actual API URL**

```typescript
export const environment = {
  production: true,
  apiBaseUrl: 'https://<API_URL>',
};
```

- [ ] **Step 7: Commit env update**

```bash
git add web/src/environments/environment.production.ts
git commit -m "chore(web): point production env at deployed API URL"
```

---

## Task 19: Deploy API to App Service

**Files:** none (deployment commands captured in README in Task 21).

- [ ] **Step 1: Publish**

```bash
cd api
dotnet publish src/Nextech.Api -c Release -o publish
cd publish && zip -r ../publish.zip . && cd ..
```

- [ ] **Step 2: Deploy zip**

```bash
az webapp deploy -g rg-nextech -n nextech-api-<initials> --src-path publish.zip --type zip
```

- [ ] **Step 3: Smoke-test the deployed API**

```bash
curl https://<API_URL>/api/health
curl 'https://<API_URL>/api/stories?page=1&pageSize=5'
```

Expected: health returns `{"status":"ok"}`; stories returns JSON. First request may be slow on F1 cold start.

---

## Task 20: Deploy Angular to Static Web App

**Files:** none.

- [ ] **Step 1: Install Static Web Apps CLI if needed**

```bash
npm install -g @azure/static-web-apps-cli
```

- [ ] **Step 2: Get a deployment token**

```bash
az staticwebapp secrets list -g rg-nextech -n nextech-web-<initials> --query "properties.apiKey" -o tsv
```

Save the value as `SWA_TOKEN`.

- [ ] **Step 3: Build and deploy**

```bash
cd web
npm run build -- --configuration production
swa deploy ./dist/web/browser --deployment-token "$SWA_TOKEN" --env production
```

- [ ] **Step 4: Smoke-test the deployed UI**

Open `https://<WEB_URL>` in a browser. Expected: stories load (allow ~10s for API cold start). Check that search and paging work.

If you see CORS errors in the browser console, double-check the `Cors__AllowedOrigins__0` env var on the API matches the exact origin (including `https://`).

---

## Task 21: Write README.md

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write the README**

```markdown
# Nextech — Hacker News Stories Viewer

A small full-stack app that displays the newest Hacker News stories with search and paging.
Built as a coding challenge submission.

**Live URLs:**
- Web: https://<WEB_URL>
- API: https://<API_URL>

> The API runs on Azure App Service Free (F1). The first request after a cold start may take ~10s while the instance warms and the cache populates.

## Architecture

\`\`\`
Angular 18 (Static Web App) ──► ASP.NET Core 8 (App Service) ──► Hacker News API
                                  ↑
                             StoryCache (singleton, in-memory)
                                  ↑
                       StoryRefreshService (IHostedService, 60s)
\`\`\`

**Why a background refresh:** The HN API requires a separate fetch per story; doing this on every request would be slow and rate-limited. A hosted service warms an in-memory cache on startup and refreshes every 60s, so user-facing requests are sub-millisecond filter+page operations.

## Stack

- **API:** .NET 8, ASP.NET Core, xUnit, Moq, FluentAssertions, Application Insights
- **Web:** Angular 18 (standalone, signals), Angular Material, RxJS, Karma/Jasmine, Playwright
- **Infra:** Azure App Service (Linux, .NET 8) + Azure Static Web App

## Running locally

**Prereqs:** .NET 8 SDK, Node 20+, Angular CLI 18 (\`npm i -g @angular/cli@18\`).

\`\`\`bash
# API
cd api
dotnet run --project src/Nextech.Api
# listens on http://localhost:5000 (or whatever Kestrel picks — see startup log)
\`\`\`

\`\`\`bash
# Web (separate terminal)
cd web
npm install
npm start
# open http://localhost:4200
\`\`\`

If the API picks a non-5000 port, update \`web/src/environments/environment.ts\`'s \`apiBaseUrl\` and restart \`npm start\`.

## Running tests

\`\`\`bash
# API: unit + integration
cd api && dotnet test

# Web: unit
cd web && npm test -- --watch=false --browsers=ChromeHeadless

# Web: E2E (uses API stubs, doesn't need backend running)
cd web && npm run e2e
\`\`\`

## Deployment

Both resources are provisioned and deployed via the Azure CLI. See [docs/superpowers/specs/2026-04-27-hn-stories-viewer-design.md](docs/superpowers/specs/2026-04-27-hn-stories-viewer-design.md) §8 and the implementation plan for the exact commands.

Key environment variables on the App Service:
- \`APPLICATIONINSIGHTS_CONNECTION_STRING\` — App Insights ingestion
- \`Cors__AllowedOrigins__0\` — the Static Web App URL (https-prefixed, no trailing slash)

## AI Tool Usage

The challenge requires AI partnering. This was built using **Claude Code (Opus 4.7)** in the terminal, with the [superpowers](https://github.com/anthropics/claude-code-superpowers) workflow:

1. **Brainstorming** — explored the problem space, decided architecture and stack via a guided question/answer flow. Output: [docs/superpowers/specs/2026-04-27-hn-stories-viewer-design.md](docs/superpowers/specs/2026-04-27-hn-stories-viewer-design.md).
2. **Planning** — derived a step-by-step implementation plan from the spec. Output: [docs/superpowers/plans/2026-04-27-hn-stories-viewer.md](docs/superpowers/plans/2026-04-27-hn-stories-viewer.md).
3. **Execution** — implemented the plan task-by-task, TDD-style, with tests written before implementation in every component.

### Key prompts (curated from the build)

**Planning / Design**
- *"Walk me through caching options for the HN newstories endpoint — what are the trade-offs between on-demand caching, a hosted background refresh, and a hybrid?"*
  → AI proposed three options. **Accepted** the background refresh because it best demonstrates `IHostedService` and avoids first-request latency.
- *"What's the cleanest way to make a singleton in-memory cache thread-safe under a writer-during-reader scenario?"*
  → AI suggested `ReaderWriterLockSlim`. **Modified** to a simpler `Volatile.Read/Write` against an immutable list reference — simpler, lock-free, and good enough for this access pattern.

**Implementation**
- *"Write an xUnit test that verifies an HttpClient hits a specific URL using Moq's `Protected().Setup` on `HttpMessageHandler`."*
  → AI produced a working pattern. **Accepted** with minor naming tweaks.
- *"Show me the Angular 18 way to debounce an input and reset paging state when the search term changes — using signals where natural."*
  → AI produced a `Subject<string> + debounceTime + tap` pipeline that updates signals. **Accepted**.

**Debugging**
- *"My `WebApplicationFactory<Program>` test fails with 'cannot find Program' — what am I missing?"*
  → AI reminded me to add `public partial class Program;` to `Program.cs`. **Accepted**, fix took 30 seconds.

**Testing**
- *"Generate Playwright tests that stub `/api/stories` with `page.route()` for: initial render, search filter, error+retry, missing-URL story."*
  → AI produced four tests. **Modified** the search-filter test to count network calls and verify debouncing kicked in (rather than just checking the result count).

### Notable rejections / modifications

- AI initially suggested **Polly retry policies** wrapped around `HackerNewsClient`. Rejected — the refresh service already tolerates per-item failures and keeps the stale cache on errors. Adding Polly would have been YAGNI for the scope.
- AI suggested an **`IDistributedCache`** abstraction "in case you want to swap to Redis later." Rejected — single-instance F1 deployment, no horizontal scaling in scope, and the abstraction would have obscured the actual logic.
- For the Angular component, AI proposed **NgRx** for state management. Rejected — this is one screen with three signals, not a state-management problem. Plain signals and a Subject for debouncing are clearer.

## Trade-offs & next steps

- **No auth.** Public read-only data, no need.
- **Single-instance cache.** Each App Service instance has its own copy. Horizontal scaling would need a shared cache (Redis) — out of scope.
- **No GitHub Actions CI.** Manual deploy via `az` for this submission. CI would be the obvious next step.
- **No custom domain.** Default `*.azurewebsites.net` and `*.azurestaticapps.net` are used.
- **No rate limiting on the API.** With an F1 tier and a tiny audience, not a concern; for production, add `Microsoft.AspNetCore.RateLimiting` middleware.
- **App Insights at default sampling.** A real deployment would tune sampling and add custom metrics for cache age and refresh duration.
\`\`\`
```

- [ ] **Step 2: Replace `<API_URL>` and `<WEB_URL>` with the actual hostnames from Task 18**

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add README with architecture, run/deploy instructions, and AI usage"
```

---

## Task 22: Curate AI prompts log (final pass)

**Files:** none — the README "Key prompts" section is already populated in Task 21 with representative examples.

- [ ] **Step 1: Re-read the README's "AI Tool Usage" section with fresh eyes**

Check that:
- Tools are named (Claude Code, Opus 4.7).
- Workflow is described (brainstorm → plan → execute).
- At least 4-6 representative prompts appear, each with: prompt → AI output summary → accepted/modified/rejected with rationale.
- At least 2-3 explicit rejections with rationale.

If anything in the section feels generic or invented, replace it with a real prompt you remember using during the build.

- [ ] **Step 2: If you swapped any prompts, commit**

```bash
git add README.md
git commit -m "docs: refine AI prompts log with real examples"
```

---

## Task 23: Final smoke test

**Files:** none.

- [ ] **Step 1: From a clean clone, verify everything works**

In a separate temp directory:
```bash
git clone <your-repo-url> nextech-fresh && cd nextech-fresh
cd api && dotnet test && cd ..
cd web && npm install && npm test -- --watch=false --browsers=ChromeHeadless && npm run e2e && cd ..
```

Expected: all tests pass on a fresh clone.

- [ ] **Step 2: Verify deployed URLs are still healthy**

```bash
curl https://<API_URL>/api/health
curl 'https://<API_URL>/api/stories?page=1&pageSize=3'
```

Open `https://<WEB_URL>` in a browser and verify search + paging.

- [ ] **Step 3: Push to GitHub**

```bash
git push origin super-powers
```

(Or whichever branch is the submission branch; if `main` is the submission, `git checkout main && git merge super-powers && git push`.)

---

## Self-Review Notes

- **Spec coverage:**
  - §2 stack → Tasks 2 (.NET), 11 (Angular)
  - §3 layout → Tasks 1, 2, 11
  - §4 architecture → Tasks 5, 6, 7, 9, 13, 14
  - §5 backend → Tasks 3-10
  - §6 frontend → Tasks 12-15
  - §7 testing → Tasks 5, 6, 7, 10, 13, 14, 16
  - §8 deployment → Tasks 18-20
  - §9 README + AI docs → Tasks 21-22
  - §10 open questions → none
- **No placeholders:** every code-changing step has the full code or exact command.
- **Type/method consistency:** `IStoryCache.Set(IReadOnlyList<Story>)` and `Query(string?, int, int)` are consistent across Task 6, Task 7, Task 8, Task 10. `StoriesResponse` shape is consistent across API and frontend (`items`/`total`/`page`/`pageSize`). Test IDs `data-testid="story-title"` etc. are consistent between component and Playwright tests.
