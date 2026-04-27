# Nextech — Hacker News Newest Stories

A full-stack web app that displays the newest stories from [Hacker News](https://news.ycombinator.com/newest), with live search and pagination.

**Live:**
- Frontend: https://wonderful-field-00df44c0f.7.azurestaticapps.net
- API: https://tom-nextech-api.ambitiousbush-5c2916fd.eastus.azurecontainerapps.io/api/stories

---

## Architecture

```
Angular 18 SPA  →  ASP.NET Core API  →  Hacker News Firebase API
(Static Web App)   (Container App)       (background refresh)
```

The API runs a background service (`StoryRefreshService`) that fetches all 500 newest story IDs and hydrates each item in parallel on startup, then refreshes every 60 seconds. Stories are held in a lock-free in-memory cache (`StoryCache`) using `Volatile.Read/Write` for atomic reference swaps. The controller serves search and paging entirely from cache — no per-request I/O.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 18, standalone components, Angular Material, RxJS |
| API | ASP.NET Core (.NET 10), C# |
| Testing (API) | xUnit, Moq, FluentAssertions, WebApplicationFactory |
| Testing (Frontend) | Karma/Jasmine, Playwright |
| Infra (IaC) | Azure Bicep |
| Hosting | Azure Container Apps (API), Azure Static Web Apps (frontend) |
| Observability | Azure Application Insights |

## Repository Layout

```
nextech/
├── api/
│   ├── Dockerfile
│   ├── src/Nextech.Api/
│   │   ├── Controllers/        # StoriesController
│   │   ├── HackerNews/         # Client, cache, refresh service
│   │   └── Models/             # Story, StoriesResponse, HnItem
│   └── tests/
│       ├── Nextech.Api.UnitTests/
│       └── Nextech.Api.IntegrationTests/
├── web/
│   ├── src/app/
│   │   ├── models/             # Story, StoriesResponse interfaces
│   │   ├── services/           # StoryService (HttpClient wrapper)
│   │   └── stories/            # StoriesComponent (search + paging UI)
│   └── e2e/                    # Playwright E2E tests
└── infra/
    └── main.bicep              # All Azure resources
```

## Local Development

### Prerequisites
- .NET 10 SDK
- Node.js 20+
- Docker (for container builds)

### API

```bash
cd api
dotnet run --project src/Nextech.Api
# Listening on http://localhost:5000
# GET http://localhost:5000/api/stories?search=&page=1&pageSize=20
# GET http://localhost:5000/api/health
```

### Frontend

```bash
cd web
npm install
npx ng serve
# http://localhost:4200 (proxies /api to localhost:5000)
```

## Testing

### API — unit + integration (23 tests)

```bash
cd api
dotnet test
```

### Frontend — Karma unit tests (14 tests)

```bash
cd web
npx ng test --watch=false --browsers=ChromeHeadless
```

### Frontend — Playwright E2E (7 tests, no backend required)

```bash
cd web
npx playwright test
```

## Infrastructure

All Azure resources are defined in `infra/main.bicep`:

- **Log Analytics workspace** — backing store for App Insights
- **Application Insights** — telemetry (one custom log per cache refresh)
- **Azure Container Registry** (`nextechregistry`) — stores the API Docker image
- **Container Apps Environment** (`nextech-env`) — hosting environment
- **Container App** (`tom-nextech-api`) — API, scales to 0 when idle
- **Static Web App** (`nextech-web`) — Angular frontend, global CDN

### Deploy infrastructure

```bash
az login
az group create --name nextech-rg --location eastus
az deployment group create --resource-group nextech-rg --template-file infra/main.bicep
```

### Deploy API

```bash
az acr login --name nextechregistry
docker buildx build --platform linux/amd64 -t nextechregistry.azurecr.io/nextech-api:latest --load api/
docker push nextechregistry.azurecr.io/nextech-api:latest
az containerapp update \
  --name tom-nextech-api \
  --resource-group nextech-rg \
  --image nextechregistry.azurecr.io/nextech-api:latest
```

> Note: build requires `--platform linux/amd64` on Apple Silicon. The Dockerfile uses `FROM --platform=$BUILDPLATFORM` for the SDK stage to avoid QEMU emulation issues with MSBuild.

### Deploy frontend

```bash
cd web
npx ng build --configuration=production
DEPLOY_TOKEN=$(az staticwebapp secrets list --name nextech-web --resource-group nextech-rg --query "properties.apiKey" -o tsv)
swa deploy dist/web/browser --deployment-token "$DEPLOY_TOKEN" --env production
```

## API Reference

### `GET /api/stories`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `search` | string | — | Case-insensitive title substring filter |
| `page` | int | 1 | 1-based page number |
| `pageSize` | int | 20 | Items per page (1–100) |

**Response:**
```json
{
  "items": [{ "id": 1, "title": "...", "url": "...", "by": "user", "time": 1700000000, "score": 42 }],
  "total": 312,
  "page": 1,
  "pageSize": 20
}
```

### `GET /api/health`

Returns `{"status":"ok"}` when the API is running.
