# Error Logging Design

**Date:** 2026-04-27  
**Status:** Approved

## Goal

Add structured error logging across the full stack — Angular frontend and ASP.NET Core API — so that errors in production are captured in Application Insights and visible to developers locally via the browser console.

## Architecture

Both surfaces report to the same Application Insights resource already used by the API. The AI connection string is safe to expose in the Angular environment files; the browser SDK is designed for client-side use.

```
Angular frontend                     ASP.NET Core API
─────────────────                    ─────────────────
ErrorHandler ──┐                     HackerNewsClient  (add ILogger)
HTTP Interceptor┤──► AppInsights ◄──► StoriesController (no change)
                │    (shared          StoryRefreshService (already has it)
                └── resource)
```

## API Changes

### `HackerNewsClient`

**What:** Add `ILogger<HackerNewsClient>`. In `GetNewStoryIdsAsync` and `GetItemAsync`, call `LogError` with the exception and the endpoint path before re-throwing with `throw;`.

**Why:** Exceptions propagate unchanged to `StoryRefreshService` where they are already caught and logged as `LogWarning`. The additional log at the client level adds the endpoint URL as context. No try/catch blocks are added or changed.

**What stays the same:** `StoriesController` — no changes. `_cache.Query()` is pure in-memory and will not throw. If an unexpected exception did escape, Application Insights captures it at the ASP.NET middleware level automatically.

## Frontend Changes

### 1. `AppInsightsService`

- **File:** `web/src/app/services/app-insights.service.ts`
- **Responsibility:** Initialize `@microsoft/applicationinsights-web` once. Expose the `appInsights` instance (nullable).
- **Guard:** If `environment.appInsightsConnectionString` is empty or absent, skip SDK initialization entirely — no warnings, no network calls. The exposed instance is `null`.
- **Registration:** Eagerly initialized via `APP_INITIALIZER` in `app.config.ts`.

### 2. `GlobalErrorHandler`

- **File:** `web/src/app/global-error-handler.ts`
- **Responsibility:** Implements Angular's `ErrorHandler`. On every unhandled error:
  1. Always calls `console.error(error)` — local dev sees errors in browser devtools.
  2. If AI is initialized, calls `appInsights.trackException({ exception: error })`.
- **Registration:** Provided in `app.config.ts` as `{ provide: ErrorHandler, useClass: GlobalErrorHandler }`.

### 3. `ErrorLoggingInterceptor`

- **File:** `web/src/app/interceptors/error-logging.interceptor.ts`
- **Responsibility:** Functional HTTP interceptor. On HTTP errors:
  1. Extracts `url` and `status` from the `HttpErrorResponse`.
  2. Always calls `console.error` with url, status, and the error object.
  3. If AI is initialized, calls `appInsights.trackException` with url and status as custom properties.
  4. Re-throws the error — `StoriesComponent` error signal behavior is unchanged.
- **Registration:** Added to `withInterceptors([errorLoggingInterceptor])` in `app.config.ts`.

### 4. Environment Configuration

| File | Key | Value |
|------|-----|-------|
| `environment.ts` | `appInsightsConnectionString` | `''` (empty — AI dormant in local dev) |
| `environment.prod.ts` | `appInsightsConnectionString` | Real AI connection string |

### Local Development Behaviour

AI is completely dormant when the connection string is empty. Errors surface in the browser console from both the `GlobalErrorHandler` and `ErrorLoggingInterceptor` — richer context than today (HTTP status + URL is included). No config changes required to run locally.

## Dependencies

- **New npm package:** `@microsoft/applicationinsights-web`
- **No new backend packages** — `ILogger` is already available via ASP.NET Core DI.

## Cost Controls

**Daily ingestion cap:** Set a daily data cap on the Application Insights resource in the Azure portal (Monitor → Application Insights → Usage and estimated costs → Daily cap). Once the cap is hit, AI stops ingesting for the remainder of the day. This is the primary guard against runaway costs from a traffic flood. Recommended starting value: 0.1 GB/day for a low-traffic site.

This is a one-time manual step in the portal, not a code change.

## Out of Scope

- Router change tracking (not needed — app has a single view)
- Performance/page-view telemetry
- Client-side error throttling
