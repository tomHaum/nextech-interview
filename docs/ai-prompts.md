# AI Prompts Log

Notable interactions with Claude during development of this project. Captures decisions, corrections, and pivots where AI assistance was meaningful.

---

## 1. Initial architecture design

**Prompt:** Brainstorm a Hacker News newest stories app with search and paging — Angular + ASP.NET Core, finish today, solid polish.

**Decision points resolved:**
- Flat monorepo (`/api` + `/web`) over separate repos
- Background `IHostedService` cache (fetch all 500 stories on startup, refresh every 60s) over on-demand fetching
- Server-side search/paging over client-side (cache already in memory, keeps API surface clean)
- Angular Material UI, xUnit + WebApplicationFactory + Playwright testing
- Azure App Service + Static Web App (later changed — see entry 4)

**Outcome:** Full spec and implementation plan written before any code was touched.

---

## 2. InternalsVisibleTo and the public/internal tension

**Prompt:** Subagent made `HnItem` public to fix a compiler error. Why did that happen?

**Root cause:** ASP.NET Core requires `StoriesController` to be `public`. `StoriesController` depends on `IStoryCache`, so `IStoryCache` must also be `public` (CS0051). Meanwhile, `HnItem`, `HackerNewsClient`, `StoryCache`, and `StoryRefreshService` can stay `internal` — they're never referenced from outside the assembly.

**Fix:** Added `InternalsVisibleTo` for both test assemblies and `DynamicProxyGenAssembly2` (needed for Moq to proxy internal interfaces) via `AssemblyAttribute` in the `.csproj`. Restored all internal types.

**Outcome:** Clean encapsulation boundary — only types that genuinely cross assembly lines are public.

---

## 3. Integration test factory and Application Insights

**Prompt:** Implement `CustomWebApplicationFactory` for integration tests using `WebApplicationFactory<Program>`.

**Problem encountered:** `AddApplicationInsightsTelemetry()` throws `InvalidOperationException: A connection string was not found` at host startup — even in tests, even with no real telemetry destination.

**Fix:** Injected a fake (non-routing) connection string via `ConfigureAppConfiguration` using `AddInMemoryCollection` in the factory. No network calls are made; the key just satisfies the SDK's startup validation.

**Outcome:** Integration tests start cleanly with no network dependency on Azure.

---

## 4. Pivot to infrastructure as code (Bicep)

**Prompt (user):** "Can we use Bicep? I want to make sure it's all IaC."

**Context:** The original plan was to provision resources with imperative `az` CLI commands. After explaining the difference between Bicep (declarative, idempotent, version-controlled) and raw CLI (imperative, requires manual "already exists" handling), the user chose Bicep.

**Decision:** All Azure resources defined in `infra/main.bicep`. The entire stack deploys with two commands: `az group create` + `az deployment group create`. Resources are reproducible and diff-able in git.

**Outcome:** IaC-first approach meant the App Service → Container Apps pivot (see entry 5) was a clean file edit rather than a series of CLI deletions and recreations.

---

## 5. Infrastructure pivot: App Service → Container Apps

**Prompt:** Provision Azure resources using Bicep.

**Problem encountered:** New Azure free trial subscriptions have a hard quota of 0 for App Service VMs (both B1 and F1 tiers) until Microsoft initialises them — affects both CLI and portal. Provider registration was not the cause.

**Decision:** Switched API hosting to Azure Container Apps (Consumption plan) + Azure Container Registry (Basic). Container Apps has no VM quota restriction and a genuine free tier.

**What changed in Bicep:** Removed `Microsoft.Web/serverfarms` + `Microsoft.Web/sites`; added `Microsoft.ContainerRegistry/registries`, `Microsoft.App/managedEnvironments`, `Microsoft.App/containerApps`. Added `api/Dockerfile`.

**Outcome:** Full stack deployed successfully. See `infra/main.bicep` and `infra/project_infra_pivot.md` in memory for details.

---

## 6. Docker multi-platform build: ARM → amd64

**Prompt:** Build and push Docker image for the Container App.

**Problem encountered:** Mac builds `linux/arm64` by default. Azure Container Apps only accepts `linux/amd64`. First attempt with `--platform linux/amd64` failed because QEMU emulation caused MSBuild to throw `MSB4184: [MSBuild]::GetTargetFrameworkVersion("net6.0")` during `dotnet restore` — a known issue with the Application Insights MSBuild targets under emulation.

**Fix:** Used `FROM --platform=$BUILDPLATFORM` for the SDK stage so the build runs natively on the host (ARM), with cross-compilation producing the amd64 output. This is the correct multi-stage Docker pattern for cross-platform builds.

**Outcome:** Clean amd64 image built and pushed to ACR without emulation.

---

## 7. Pagination E2E test gap caught by user

**Prompt (user):** "What happened to the pagination E2E tests?"

**Gap:** The generated Playwright suite included a "paginator is visible" check but no test that actually exercises page navigation behaviour.

**Fix added:** A test that stubs the API with `total: 50` (enabling the next-page button), clicks it, and asserts the second API request contains `page=2` via `page.waitForRequest()`.

**Outcome:** Meaningful behavioural coverage rather than a presence check.

---

## 8. Rejecting the initial UI design

**Prompt (user):** "The UI looks pretty basic — can we do a full visual refresh?"

**What AI initially produced:** The original design spec proposed: relative timestamps, a `(no link)` subtext on URL-less stories, a `mat-progress-bar` instead of a spinner, an empty state message, and autofocus on the search field. All reasonable additions — but the visual direction was still the default Angular Material skeleton (indigo-pink prebuilt theme, `mat-card` wrapper, `mat-list-item` rows). Functional, but generic.

**Rejection:** The spec's visual direction was rejected as too close to the Material boilerplate. The reasoning: the challenge evaluates "elegance and structure of the design" — a default theme signals no intentionality.

**What was built instead:** A full visual refresh using approach "1+3": a custom Angular Material M3 theme with HN orange (`mat.$orange-palette`) as the primary palette, a `mat-toolbar` app bar, and custom `<ul>/<li>` story rows (dropping `mat-list-item` for full CSS control). The result: HN-branded orange throughout, left accent bars, warm hover state, and a grey page background behind the white card.

**Outcome:** The AI's spec gaps (empty state, relative time, no-link badge, progress bar) were all kept and implemented. The visual presentation was rebuilt from scratch with an intentional design direction rather than accepting the default skeleton.

