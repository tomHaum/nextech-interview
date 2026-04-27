# Update Stale Documentation — Container Apps Pivot

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update the design spec and implementation plan to reflect the Container Apps + ACR infrastructure that was actually built, replacing all stale App Service references.

**Architecture:** Documentation-only changes. No code, no tests. Two source files need targeted edits; the memory file needs a status update.

**Tech Stack:** Markdown editing only.

---

## Files

- Modify: `docs/superpowers/specs/2026-04-27-hn-stories-viewer-design.md`
- Modify: `docs/superpowers/plans/2026-04-27-hn-stories-viewer.md`
- Modify: `/Users/thomashaumersen/.claude/projects/-Users-thomashaumersen-workspace-nextech/memory/project_infra_pivot.md`

---

## Task 1: Update design spec

**Files:**
- Modify: `docs/superpowers/specs/2026-04-27-hn-stories-viewer-design.md`

- [ ] **Step 1: Update Tech Stack section (line 16, 21)**

Change line 16:
```
- **API:** .NET 8 LTS, ASP.NET Core, C# 12
```
to:
```
- **API:** .NET 10, ASP.NET Core, C# 13
```

Change line 21:
```
- **Hosting:** Azure App Service (Linux, .NET 8) + Azure Static Web App
```
to:
```
- **Hosting:** Azure Container Apps (Consumption) + Azure Container Registry (Basic) + Azure Static Web App
```

- [ ] **Step 2: Update architecture diagram (line 60)**

Change:
```
   Azure Static Web App            Azure App Service (Linux)
```
to:
```
   Azure Static Web App            Azure Container Apps
```

- [ ] **Step 3: Update health endpoint note (line 108)**

Change:
```
`GET /api/health` → `200 OK` with `{ "status": "ok" }`. Used by App Service's health probe.
```
to:
```
`GET /api/health` → `200 OK` with `{ "status": "ok" }`. Used by Container Apps health probe.
```

- [ ] **Step 4: Update CORS config note (line 149)**

Change:
```
In production, `Cors:AllowedOrigins:0` is overridden via App Service env var to the Static Web App URL.
```
to:
```
In production, `Cors:AllowedOrigins:0` is overridden via Container App env var to the Static Web App URL.
```

- [ ] **Step 5: Rewrite Section 8 (Deployment)**

Replace the entire Section 8 (lines 267–286 — from `### 8.1 Resources` through the end of `### 8.3 First-request expectation`) with:

```markdown
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
```

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/specs/2026-04-27-hn-stories-viewer-design.md
git commit -m "docs(spec): update deployment section to reflect Container Apps + ACR"
```

---

## Task 2: Update implementation plan

**Files:**
- Modify: `docs/superpowers/plans/2026-04-27-hn-stories-viewer.md`

- [ ] **Step 1: Update Tech Stack header (line 9)**

Change:
```
**Tech Stack:** .NET 8, ASP.NET Core, xUnit, FluentAssertions, Moq, Application Insights · Angular 18 (standalone, signals), Angular Material, RxJS, Karma/Jasmine, Playwright · Azure App Service + Static Web App.
```
to:
```
**Tech Stack:** .NET 10, ASP.NET Core, xUnit, FluentAssertions, Moq, Application Insights · Angular 18 (standalone, signals), Angular Material, RxJS, Karma/Jasmine, Playwright · Azure Container Apps + ACR + Static Web App.
```

- [ ] **Step 2: Update the stale production apiUrl placeholder (line 1287)**

Change:
```typescript
  apiBaseUrl: 'https://nextech-api.azurewebsites.net',
```
to:
```typescript
  apiUrl: 'https://tom-nextech-api.ambitiousbush-5c2916fd.eastus.azurecontainerapps.io',
```

Change the note on line 1291:
```
(The actual production URL gets updated in Task 18 after the App Service is created.)
```
to:
```
(The actual production URL is set after Task 18 deploys the Container App.)
```

- [ ] **Step 3: Rewrite Task 18 (starting at line 2079)**

Replace the Task 18 content from `## Task 18: Provision Azure resources` through the end of that task's steps with:

```markdown
## Task 18: Provision Azure resources (Bicep)

**Files:**
- Create: `infra/main.bicep`

All resources are defined in `infra/main.bicep` and provisioned with two commands. See the spec §8.5 for why Container Apps was chosen over App Service.

Resources created:
- Log Analytics workspace (`nextech-logs`)
- Application Insights (`nextech-insights`)
- Azure Container Registry (`nextechregistry`, Basic)
- Container Apps Environment (`nextech-env`, Consumption)
- Container App (`tom-nextech-api`, placeholder image, scales to 0)
- Static Web App (`nextech-web`, Free)

- [ ] **Step 1: Log in and create resource group**

```bash
az login
az group create --name nextech-rg --location eastus
```
Expected: `"provisioningState": "Succeeded"`

- [ ] **Step 2: Register required providers**

```bash
az provider register --namespace Microsoft.App --wait
az provider register --namespace Microsoft.ContainerRegistry --wait
az provider register --namespace Microsoft.Insights --wait
```

- [ ] **Step 3: Deploy Bicep template**

```bash
az deployment group create --resource-group nextech-rg --template-file infra/main.bicep
```
Expected: `"provisioningState": "Succeeded"`

- [ ] **Step 4: Capture outputs**

```bash
az deployment group show --resource-group nextech-rg --name main --query "properties.outputs" -o json
```
Expected output includes `apiUrl`, `webUrl`, `acrLoginServer`, `acrName`.

- [ ] **Step 5: Commit**

```bash
git add infra/main.bicep
git commit -m "feat(infra): provision Azure resources via Bicep"
```
```

- [ ] **Step 4: Rewrite Task 19 (starting at line 2156)**

Replace the Task 19 content from `## Task 19: Deploy API to App Service` through its final step with:

```markdown
## Task 19: Deploy API to Container App

**Files:**
- Create: `api/Dockerfile`

Build the Docker image and push it to ACR, then update the Container App to use it.

- [ ] **Step 1: Log in to ACR and build the image**

```bash
az acr login --name nextechregistry
cd api
docker buildx build --platform linux/amd64 -t nextechregistry.azurecr.io/nextech-api:latest --load .
```
Note: `--platform linux/amd64` required on Apple Silicon. The Dockerfile uses `FROM --platform=$BUILDPLATFORM` for the SDK stage.

Expected: `naming to nextechregistry.azurecr.io/nextech-api:latest done`

- [ ] **Step 2: Push to ACR**

```bash
docker push nextechregistry.azurecr.io/nextech-api:latest
```
Expected: `latest: digest: sha256:...`

- [ ] **Step 3: Update Container App**

```bash
az containerapp update \
  --name tom-nextech-api \
  --resource-group nextech-rg \
  --image nextechregistry.azurecr.io/nextech-api:latest
```
Expected: returns the Container App FQDN.

- [ ] **Step 4: Smoke test**

```bash
curl https://tom-nextech-api.ambitiousbush-5c2916fd.eastus.azurecontainerapps.io/api/health
```
Expected: `{"status":"ok"}`

- [ ] **Step 5: Commit**

```bash
git add api/Dockerfile
git commit -m "feat(api): add Dockerfile for Container App deployment"
```
```

- [ ] **Step 5: Update README section in the plan (around line 2236–2254)**

Change:
```
> The API runs on Azure App Service Free (F1). The first request after a cold start may take ~10s while the instance warms and the cache populates.
```
to:
```
> The API runs on Azure Container Apps (Consumption). The first request after the container scales from zero may take ~5–10s while the instance starts and the cache warms.
```

Change the architecture line:
```
Angular 18 (Static Web App) ──► ASP.NET Core 8 (App Service) ──► Hacker News API
```
to:
```
Angular 18 (Static Web App) ──► ASP.NET Core 10 (Container App) ──► Hacker News API
```

Change the Tech Stack entries:
```
- **API:** .NET 8, ASP.NET Core, xUnit, Moq, FluentAssertions, Application Insights
```
to:
```
- **API:** .NET 10, ASP.NET Core, xUnit, Moq, FluentAssertions, Application Insights
```

```
- **Infra:** Azure App Service (Linux, .NET 8) + Azure Static Web App
```
to:
```
- **Infra:** Azure Container Apps + ACR + Azure Static Web App (all via Bicep)
```

Change the prereqs line:
```
**Prereqs:** .NET 8 SDK, Node 20+, Angular CLI 18 (`npm i -g @angular/cli@18`).
```
to:
```
**Prereqs:** .NET 10 SDK, Node 20+, Docker Desktop, Azure CLI.
```

- [ ] **Step 6: Update trade-offs section (around line 2337)**

Change:
```
- **Single-instance cache.** Each App Service instance has its own copy. Horizontal scaling would need a shared cache (Redis) — out of scope.
```
to:
```
- **Single-instance cache.** Each Container App replica has its own copy. Horizontal scaling would need a shared cache (Redis) — out of scope.
```

Change:
```
- **No custom domain.** Default `*.azurewebsites.net` and `*.azurestaticapps.net` are used.
```
to:
```
- **No custom domain.** Default `*.azurecontainerapps.io` and `*.azurestaticapps.net` are used.
```

- [ ] **Step 7: Commit**

```bash
git add docs/superpowers/plans/2026-04-27-hn-stories-viewer.md
git commit -m "docs(plan): update stale App Service references to Container Apps"
```

---

## Task 3: Mark memory as resolved

**Files:**
- Modify: `/Users/thomashaumersen/.claude/projects/-Users-thomashaumersen-workspace-nextech/memory/project_infra_pivot.md`

- [ ] **Step 1: Update the "How to apply" note**

Change the final line:
```
**How to apply:** If discussing deployment or the original design doc, note that the live infrastructure uses Container Apps, not App Service. The spec/design doc still says App Service — that's stale.
```
to:
```
**How to apply:** Infrastructure uses Container Apps + ACR, not App Service. Design spec and plan doc have been updated (as of 2026-04-27) to reflect this. No further doc work needed.
```

- [ ] **Step 2: Done — no commit needed (memory files are not in the repo)**
