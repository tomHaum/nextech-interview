# Hacker News Stories Viewer — Design

**Date:** 2026-04-27
**Author:** Tom Haumersen (with AI partnering)
**Status:** Approved for implementation
**Source challenge:** [docs/challenge.md](../../challenge.md)

## 1. Goal & Posture

Build an Angular front-end + ASP.NET Core API that displays the newest Hacker News stories with search and paging, deployable to Azure. The submission targets a "Solid Polish" bar — thoughtful architecture, meaningful tests at every layer, deployed to Azure, and a substantive README documenting AI partnering. Built in a single day.

Out of scope: authentication, comments view, story details page, dark mode, GitHub Actions CI, custom domains, multi-instance cache (Redis), rate limiting.

## 2. Tech Stack

- **API:** .NET 10, ASP.NET Core, C# 13
- **Web:** Angular 18 LTS (standalone components, signals, new control flow), TypeScript 5
- **UI:** Angular Material 18
- **API testing:** xUnit, Moq, FluentAssertions, `WebApplicationFactory` for integration
- **Web testing:** Karma/Jasmine for unit, Playwright for E2E
- **Hosting:** Azure Container Apps (Consumption) + Azure Container Registry (Basic) + Azure Static Web App
- **Observability:** Application Insights (default ASP.NET Core auto-collection + one custom log per cache refresh)

## 3. Repository Layout

Flat monorepo at the root:

```
nextech/
├── README.md                    # the user-facing doc graded by reviewers
├── api/
│   ├── Nextech.Api.sln
│   ├── src/Nextech.Api/
│   └── tests/
│       ├── Nextech.Api.UnitTests/
│       └── Nextech.Api.IntegrationTests/
├── web/
│   ├── package.json
│   ├── angular.json
│   ├── playwright.config.ts
│   ├── src/
│   └── e2e/
└── docs/
    ├── challenge.md             # the original challenge
    └── superpowers/
        ├── specs/               # this document
        └── plans/               # implementation plan (next step)
```

## 4. Architecture

```
┌──────────────────────┐        ┌────────────────────────────┐        ┌─────────────────┐
│ Angular 18 (web)     │        │ ASP.NET Core 8 API         │        │ Hacker News API │
│ - Material UI        │ HTTPS  │ - StoriesController        │ HTTPS  │ /v0/newstories  │
│ - Standalone comps   │ ─────► │ - StoryCache (singleton)   │ ─────► │ /v0/item/{id}   │
│ - Signals           │        │ - StoryRefreshService      │        │                 │
│                      │        │   (IHostedService, 60s)    │        │                 │
└──────────────────────┘        └────────────────────────────┘        └─────────────────┘
   Azure Static Web App            Azure Container Apps
```

**Data flow:**
1. On startup, `StoryRefreshService` performs an initial warm-load before the API begins serving traffic.
2. Every 60s thereafter, it refreshes the cache. On failure, the stale cache is kept and a warning is logged.
3. `StoriesController.GetStories(search, page, pageSize)` filters and paginates against the in-memory `StoryCache` — never touches HN directly. Sub-millisecond response time.
4. Angular debounces user input on the search field, calls `/api/stories`, renders results in `mat-card` list with `mat-paginator` at the bottom.

## 5. Backend Design

### 5.1 Project structure

```
api/src/Nextech.Api/
├── Program.cs
├── Controllers/
│   └── StoriesController.cs
├── HackerNews/
│   ├── IHackerNewsClient.cs
│   ├── HackerNewsClient.cs       # typed HttpClient, hits HN
│   ├── IStoryCache.cs
│   ├── StoryCache.cs             # singleton, holds warmed list, filter+page logic
│   ├── StoryRefreshService.cs    # BackgroundService
│   └── HackerNewsOptions.cs      # IOptions<T>
├── Models/
│   ├── Story.cs                  # API DTO
│   ├── StoriesResponse.cs        # { items, total, page, pageSize }
│   └── HnItem.cs                 # raw HN shape (internal)
└── appsettings.json
```

### 5.2 API surface

`GET /api/stories?search={string?}&page={int=1}&pageSize={int=20}` → `200 OK`
```json
{
  "items": [
    { "id": 123, "title": "Story title", "url": "https://...", "by": "user", "time": 1714200000, "score": 42 }
  ],
  "total": 137,
  "page": 1,
  "pageSize": 20
}
```

Validation: `page >= 1`, `1 <= pageSize <= 100`. Returns `400` on bad input.

`GET /api/health` → `200 OK` with `{ "status": "ok" }`. Used by Container Apps health probe.

### 5.3 Models

**`Story` (DTO returned to clients):**
- `int id`
- `string title`
- `string? url` (null for stories without an external link — explicitly called out by the spec)
- `string by`
- `long time` (Unix seconds)
- `int score`

**`HnItem` (internal; mirrors the HN API):** same fields plus `type`, `dead`, `deleted`. Items where `type != "story"`, `dead`, `deleted`, or `title` is null are filtered out at refresh time.

### 5.4 Caching strategy

- Single in-memory `StoryCache` singleton holds an immutable `IReadOnlyList<Story>` (stories sorted newest-first by HN's natural order from `/newstories`).
- `StoryRefreshService : BackgroundService`:
  - **First load:** synchronous in `StartAsync` — the host blocks until cache is populated before serving traffic. ~1-2s on warm-start; up to ~10s on Azure cold-start.
  - **Refresh loop:** every `RefreshIntervalSeconds` (default 60). Fetch `/newstories` → take first `MaxStories` (default 500) IDs → fetch each item with concurrency capped at 10 via `SemaphoreSlim` → filter null/dead/non-story → atomically swap the cache.
  - **Refresh failure:** log warning with details, keep the existing cache, retry next tick. Per-item fetch failures are also logged but don't abort the whole refresh.
- Filter & page operations are pure functions over the cached list:
  - Search: case-insensitive substring match on `title` only.
  - Page: 1-based; `total` is the count after filtering, not the cache size.

### 5.5 Configuration (`appsettings.json`)

```json
{
  "HackerNews": {
    "BaseUrl": "https://hacker-news.firebaseio.com/v0/",
    "MaxStories": 500,
    "RefreshIntervalSeconds": 60,
    "ItemFetchConcurrency": 10
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:4200"]
  }
}
```

In production, `Cors:AllowedOrigins:0` is overridden via Container App env var to the Static Web App URL.

### 5.6 Dependency injection

- `IHackerNewsClient` → `HackerNewsClient` via `services.AddHttpClient<IHackerNewsClient, HackerNewsClient>()` (typed client).
- `IStoryCache` → `StoryCache` (singleton — must outlive requests).
- `services.AddHostedService<StoryRefreshService>()`.
- `services.Configure<HackerNewsOptions>(config.GetSection("HackerNews"))`.
- `services.AddApplicationInsightsTelemetry()` — connection string from env var.

### 5.7 Error handling summary

| Failure | Behavior |
|---|---|
| HN returns null for an item (deleted/dead) | Filtered out at refresh |
| Per-item HTTP failure during refresh | Log warning, skip item, continue refresh |
| `/newstories` HTTP failure during refresh | Log warning, keep stale cache, retry next tick |
| Cache empty (only if first warm-load failed) | Controller returns `{ items: [], total: 0, ... }` with 200 |
| Bad query params | 400 |

## 6. Frontend Design

### 6.1 Project structure

```
web/src/
├── main.ts
├── index.html
├── styles.scss
├── app/
│   ├── app.component.ts          # shell: toolbar + <router-outlet>
│   ├── app.config.ts             # providers: HttpClient, router, animations
│   ├── app.routes.ts             # single route → StoriesComponent
│   └── stories/
│       ├── stories.component.ts  # standalone, signals
│       ├── stories.component.html
│       ├── stories.component.scss
│       ├── story.service.ts      # HTTP wrapper around /api/stories
│       └── story.model.ts        # Story, StoriesResponse types
└── environments/
    ├── environment.ts            # apiBaseUrl: 'http://localhost:5000'
    └── environment.production.ts # apiBaseUrl: '<API_URL>'
```

### 6.2 Components

**`StoriesComponent` (standalone)** — single page. Holds:
- Signals: `searchTerm`, `page`, `pageSize`, `result` (latest API response), `loading`, `error`.
- Inputs: `mat-form-field` search box (debounced 300ms via RxJS `debounceTime`), `mat-paginator` for paging (page-size options `[10, 20, 50]`, default 20).
- Output: `mat-card` per story with title-as-link (or plain `<span>` with "(no link)" subtext if `url` is null), small meta line for author + age (relative time, e.g., "3h ago").

**Interaction:**
- Search and page changes both trigger a fresh fetch.
- Changing the search term resets `page` to 1.
- Loading: subtle `mat-progress-bar` at top of list.
- Error: small inline `mat-card` with retry button.
- Empty: friendly "no stories found" message inside the list area.

### 6.3 Service

**`StoryService`** — single method:
```typescript
getStories(search: string, page: number, pageSize: number): Observable<StoriesResponse>
```
HttpClient call to `${environment.apiBaseUrl}/api/stories`. No client-side caching — server is fast.

### 6.4 A11y / UX details

- External links open in `_blank` with `rel="noopener noreferrer"`.
- Search field has autofocus.
- Paginator is Material's built-in (already accessible).
- Title for stories without URL: plain `<span>` with `(no link)` muted subtext, not a non-functional anchor.

## 7. Testing Strategy

### 7.1 Backend unit tests (`Nextech.Api.UnitTests`)

- **`StoryCacheTests`** — case-insensitive title substring filter; paging math (page 1, last page partial, beyond-last-page returns empty); `total` reflects filtered count not cache size; empty cache returns empty page.
- **`StoryRefreshServiceTests`** — fetches IDs and respects `MaxStories`; null/dead items filtered; per-item failure doesn't abort refresh; `/newstories` HTTP failure keeps stale cache.
- **`HackerNewsClientTests`** — mocked `HttpMessageHandler` verifies URL shape and JSON deserialization (including null `url` field).

### 7.2 Backend integration tests (`Nextech.Api.IntegrationTests`)

Custom `WebApplicationFactory` swaps `IHackerNewsClient` with a fake returning deterministic test data (no live HN dependency).

- `GET /api/stories` → 200, expected JSON shape, default page/pageSize.
- `?search=foo` filters by title substring.
- `?page=2&pageSize=5` returns the right slice; `total` matches.
- `?page=0` and `?pageSize=101` → 400.
- `GET /api/health` → 200.

### 7.3 Frontend unit tests (Karma/Jasmine)

- **`StoryService`** — `HttpTestingController` verifies URL + query params, response mapping, error propagation.
- **`StoriesComponent`** —
  - Renders list when service returns items.
  - Renders title as `<a>` when `url` present, plain text otherwise.
  - Search input debounces (`fakeAsync` + `tick`) and resets page to 1.
  - Paginator change triggers fetch with new page/pageSize.
  - Empty / loading / error states render correctly.

### 7.4 Frontend E2E tests (Playwright, `web/e2e/`)

Playwright launches the Angular dev server and stubs `/api/stories` via `page.route()` with fixture responses (no real backend dependency).

- Initial load shows stories.
- Typing in search updates results (debounced).
- Clicking next page on paginator updates the list.
- Story without URL renders as text, not link.
- API error shows error UI with retry button; clicking retry refetches.

### 7.5 Run commands (documented in README)

- `dotnet test` from `/api` — both unit and integration projects.
- `npm test -- --watch=false --browsers=ChromeHeadless` from `/web` — unit.
- `npm run e2e` from `/web` — Playwright.

## 8. Deployment (Azure)

### 8.1 Resources

All resources defined in `infra/main.bicep` and provisioned with two commands:

```bash
az group create --name nextech-rg --location eastus
az deployment group create --resource-group nextech-rg --template-file infra/main.bicep
```

- **Azure Container Registry (`nextechregistry`)** — stores the API Docker image (Basic SKU).
- **Azure Container Apps Environment (`nextech-env`)** — Consumption plan, no VM quota restrictions.
- **Container App (`tom-nextech-api`)** — hosts the API; scales to 0 when idle. Public URL: `https://tom-nextech-api.ambitiousbush-5c2916fd.eastus.azurecontainerapps.io`
- **Azure Static Web App (`nextech-web`)** — hosts the Angular build output. URL: `https://wonderful-field-00df44c0f.7.azurestaticapps.net`
- **Application Insights** — connection string injected as Container App secret via Bicep.

### 8.2 Deploy API

```bash
az acr login --name nextechregistry
docker buildx build --platform linux/amd64 -t nextechregistry.azurecr.io/nextech-api:latest --load api/
docker push nextechregistry.azurecr.io/nextech-api:latest
az containerapp update \
  --name tom-nextech-api \
  --resource-group nextech-rg \
  --image nextechregistry.azurecr.io/nextech-api:latest
```

> **Note:** `--platform linux/amd64` is required on Apple Silicon. The `Dockerfile` uses `FROM --platform=$BUILDPLATFORM` for the SDK stage to run MSBuild natively and avoid QEMU emulation errors.

### 8.3 Deploy frontend

```bash
cd web
npx ng build --configuration=production
DEPLOY_TOKEN=$(az staticwebapp secrets list --name nextech-web --resource-group nextech-rg --query "properties.apiKey" -o tsv)
swa deploy dist/web/browser --deployment-token "$DEPLOY_TOKEN" --env production
```

### 8.4 Configuration

- **CORS:** Bicep wires `Cors__AllowedOrigins__0` on the Container App to `https://${staticWebApp.properties.defaultHostname}` at deploy time — no manual step needed.
- **App Insights:** Connection string injected as a Container App secret by Bicep.
- **API URL in frontend:** `web/src/environments/environment.prod.ts` sets `apiUrl` to the Container App FQDN, baked into the Angular bundle at build time.

### 8.5 Infrastructure note: why Container Apps?

The original design targeted Azure App Service (Linux, F1). During provisioning on a fresh Azure free trial subscription, both B1 and F1 SKUs failed with `SubscriptionIsOverQuotaForSku` — Microsoft sets VM quota to 0 on new trial accounts. Container Apps Consumption plan has no such restriction and is included in the Azure free monthly grant.

## 9. README & AI Documentation

The challenge explicitly grades AI partnering, so the README must be substantive.

**Structure:**
1. **Overview** — one paragraph, live URLs, screenshot.
2. **Architecture** — diagram + a few sentences on the caching strategy.
3. **Running locally** — copy-pasteable commands (prereqs, `dotnet run`, `npm install` + `npm start`).
4. **Running tests** — the three commands from §7.5.
5. **Deployment** — Azure resources, manual deploy commands, env vars.
6. **AI Tool Usage** — the meaty section:
   - **Tools used:** Claude Code (Opus 4.7) for planning, scaffolding, test generation, debugging.
   - **Workflow:** brainstorm → spec doc (this file) → implementation plan → execution. Links to the spec and plan.
   - **Key prompts:** 6–10 representative prompts grouped by phase (Planning / Implementation / Debugging / Testing). For each: prompt → what AI produced → accepted / modified / rejected, with the why.
   - **Notable rejections/modifications:** at least 2-3 concrete examples where AI suggestions were wrong, over-engineered, or missed context. Demonstrates engineering judgment.
7. **Trade-offs & next steps** — one-line each: no auth, no GitHub Actions CI, no custom domains, single-instance cache (would need Redis to scale horizontally), no rate limiting.

**During the build:** capture prompts as we go in a scratch file (e.g., `docs/ai-prompts-raw.md`), curate the final list at the end. Avoids the "what did I prompt three hours ago" problem.

## 10. Open Questions

None at design time — all decisions captured above. Any new ambiguity that surfaces during implementation will be flagged in the implementation plan and re-confirmed before coding.
