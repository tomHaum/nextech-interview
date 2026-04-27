# Error Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add structured error logging to the API (`HackerNewsClient`) and Angular frontend (global `ErrorHandler` + HTTP interceptor), both routing to Application Insights in production and to `console.error` in local dev.

**Architecture:** The API gets `ILogger<HackerNewsClient>` injected and calls `LogError` before re-throwing — no try/catch structure changes. The Angular frontend gains an `AppInsightsService` singleton that guards AI initialization behind the connection string, a `GlobalErrorHandler` that routes all unhandled errors to console + AI, and a functional HTTP interceptor that enriches HTTP errors with URL/status before logging.

**Tech Stack:** ASP.NET Core (.NET 10), xUnit, Moq, FluentAssertions; Angular 18, Jasmine/Karma, `@microsoft/applicationinsights-web`

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `api/src/Nextech.Api/HackerNews/HackerNewsClient.cs` | Add `ILogger`, log + rethrow on error |
| Modify | `api/tests/Nextech.Api.UnitTests/HackerNewsClientTests.cs` | Add error-logging tests, update `CreateSut` |
| Modify | `web/src/environments/environment.ts` | Add `appInsightsConnectionString: ''` |
| Modify | `web/src/environments/environment.prod.ts` | Add `appInsightsConnectionString` with real value |
| Create | `web/src/app/services/app-insights.service.ts` | Initialize AI SDK, expose `trackException` |
| Create | `web/src/app/services/app-insights.service.spec.ts` | Unit tests for service |
| Create | `web/src/app/global-error-handler.ts` | Angular `ErrorHandler` impl |
| Create | `web/src/app/global-error-handler.spec.ts` | Unit tests for handler |
| Create | `web/src/app/interceptors/error-logging.interceptor.ts` | Functional HTTP interceptor |
| Create | `web/src/app/interceptors/error-logging.interceptor.spec.ts` | Unit tests for interceptor |
| Modify | `web/src/app/app.config.ts` | Register handler + interceptor |

---

## Task 1: API — Add ILogger to HackerNewsClient

**Files:**
- Modify: `api/src/Nextech.Api/HackerNews/HackerNewsClient.cs`
- Modify: `api/tests/Nextech.Api.UnitTests/HackerNewsClientTests.cs`

- [ ] **Step 1: Write the failing tests**

Add these two tests and update `CreateSut` in `api/tests/Nextech.Api.UnitTests/HackerNewsClientTests.cs`. Add `using Microsoft.Extensions.Logging;` and `using Microsoft.Extensions.Logging.Abstractions;` to the existing usings.

Update `CreateSut` to accept an optional logger:
```csharp
private static HackerNewsClient CreateSut(HttpMessageHandler handler, ILogger<HackerNewsClient>? logger = null)
{
    var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/v0/") };
    var opts = Options.Create(new HackerNewsOptions { BaseUrl = "https://example.com/v0/" });
    return new HackerNewsClient(http, opts, logger ?? NullLogger<HackerNewsClient>.Instance);
}
```

Add these two tests:
```csharp
[Fact]
public async Task GetNewStoryIdsAsync_logs_error_and_rethrows_on_http_failure()
{
    var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    handler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ThrowsAsync(new HttpRequestException("network error"));
    var mockLogger = new Mock<ILogger<HackerNewsClient>>();
    var sut = CreateSut(handler.Object, mockLogger.Object);

    await Assert.ThrowsAsync<HttpRequestException>(() =>
        sut.GetNewStoryIdsAsync(CancellationToken.None));

    mockLogger.Verify(
        x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<HttpRequestException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}

[Fact]
public async Task GetItemAsync_logs_error_and_rethrows_on_http_failure()
{
    var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    handler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ThrowsAsync(new HttpRequestException("network error"));
    var mockLogger = new Mock<ILogger<HackerNewsClient>>();
    var sut = CreateSut(handler.Object, mockLogger.Object);

    await Assert.ThrowsAsync<HttpRequestException>(() =>
        sut.GetItemAsync(42, CancellationToken.None));

    mockLogger.Verify(
        x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<HttpRequestException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd api && dotnet test tests/Nextech.Api.UnitTests --filter "GetNewStoryIdsAsync_logs_error|GetItemAsync_logs_error" -v
```
Expected: compile error — `HackerNewsClient` constructor doesn't have 3 parameters yet.

- [ ] **Step 3: Implement the changes in HackerNewsClient.cs**

Replace the entire file `api/src/Nextech.Api/HackerNews/HackerNewsClient.cs`:
```csharp
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
```

- [ ] **Step 4: Run all API unit tests**

```bash
cd api && dotnet test tests/Nextech.Api.UnitTests -v
```
Expected: all tests pass, including the two new ones.

- [ ] **Step 5: Commit**

```bash
git add api/src/Nextech.Api/HackerNews/HackerNewsClient.cs api/tests/Nextech.Api.UnitTests/HackerNewsClientTests.cs
git commit -m "feat(api): add ILogger to HackerNewsClient, log errors before rethrowing"
```

---

## Task 2: Frontend — Install package and add environment keys

**Files:**
- Modify: `web/src/environments/environment.ts`
- Modify: `web/src/environments/environment.prod.ts`

- [ ] **Step 1: Install the Application Insights browser SDK**

```bash
cd web && npm install @microsoft/applicationinsights-web
```
Expected: package added to `node_modules` and `package.json`.

- [ ] **Step 2: Add connection string key to environment.ts**

Replace the full contents of `web/src/environments/environment.ts`:
```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5013',
  appInsightsConnectionString: ''
};
```

- [ ] **Step 3: Add connection string key to environment.prod.ts**

Get the connection string from the Azure portal: Application Insights resource → Overview → Connection String (looks like `InstrumentationKey=...;IngestionEndpoint=...`).

Replace the full contents of `web/src/environments/environment.prod.ts`:
```typescript
export const environment = {
  production: true,
  apiUrl: 'https://tom-nextech-api.ambitiousbush-5c2916fd.eastus.azurecontainerapps.io',
  appInsightsConnectionString: '<paste connection string from Azure portal here>'
};
```

- [ ] **Step 4: Commit**

```bash
git add web/src/environments/environment.ts web/src/environments/environment.prod.ts web/package.json web/package-lock.json
git commit -m "feat(web): install applicationinsights-web, add connection string env key"
```

---

## Task 3: Frontend — AppInsightsService

**Files:**
- Create: `web/src/app/services/app-insights.service.ts`
- Create: `web/src/app/services/app-insights.service.spec.ts`

- [ ] **Step 1: Write the failing spec**

Create `web/src/app/services/app-insights.service.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { AppInsightsService } from './app-insights.service';

describe('AppInsightsService', () => {
  let service: AppInsightsService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AppInsightsService);
  });

  it('should not initialize AI when connection string is empty', () => {
    // environment.appInsightsConnectionString is '' in the test environment
    expect(service.appInsights).toBeNull();
  });

  it('trackException should not throw when AI is not initialized', () => {
    expect(() => service.trackException(new Error('test'))).not.toThrow();
  });

  it('trackException should not throw when called with custom properties', () => {
    expect(() =>
      service.trackException(new Error('test'), { url: '/api', status: '500' })
    ).not.toThrow();
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

```bash
cd web && ng test --include="**/app-insights.service.spec.ts" --watch=false
```
Expected: error — `AppInsightsService` not found.

- [ ] **Step 3: Implement AppInsightsService**

Create `web/src/app/services/app-insights.service.ts`:
```typescript
import { Injectable } from '@angular/core';
import { ApplicationInsights } from '@microsoft/applicationinsights-web';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AppInsightsService {
  readonly appInsights: ApplicationInsights | null;

  constructor() {
    if (!environment.appInsightsConnectionString) {
      this.appInsights = null;
      return;
    }
    this.appInsights = new ApplicationInsights({
      config: { connectionString: environment.appInsightsConnectionString }
    });
    this.appInsights.loadAppInsights();
  }

  trackException(error: Error, properties?: { [key: string]: string }): void {
    this.appInsights?.trackException({ exception: error }, properties);
  }
}
```

- [ ] **Step 4: Run the spec**

```bash
cd web && ng test --include="**/app-insights.service.spec.ts" --watch=false
```
Expected: 3 specs, 0 failures.

- [ ] **Step 5: Commit**

```bash
git add web/src/app/services/app-insights.service.ts web/src/app/services/app-insights.service.spec.ts
git commit -m "feat(web): add AppInsightsService with guarded AI initialization"
```

---

## Task 4: Frontend — GlobalErrorHandler

**Files:**
- Create: `web/src/app/global-error-handler.ts`
- Create: `web/src/app/global-error-handler.spec.ts`
- Modify: `web/src/app/app.config.ts`

- [ ] **Step 1: Write the failing spec**

Create `web/src/app/global-error-handler.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { GlobalErrorHandler } from './global-error-handler';
import { AppInsightsService } from './services/app-insights.service';

describe('GlobalErrorHandler', () => {
  let handler: GlobalErrorHandler;
  let mockAppInsights: jasmine.SpyObj<AppInsightsService>;

  beforeEach(() => {
    mockAppInsights = jasmine.createSpyObj<AppInsightsService>('AppInsightsService', ['trackException']);
    TestBed.configureTestingModule({
      providers: [
        GlobalErrorHandler,
        { provide: AppInsightsService, useValue: mockAppInsights }
      ]
    });
    handler = TestBed.inject(GlobalErrorHandler);
    spyOn(console, 'error');
  });

  it('should call console.error with the error', () => {
    const err = new Error('boom');
    handler.handleError(err);
    expect(console.error).toHaveBeenCalledWith(err);
  });

  it('should call trackException with the error when it is an Error instance', () => {
    const err = new Error('boom');
    handler.handleError(err);
    expect(mockAppInsights.trackException).toHaveBeenCalledWith(err);
  });

  it('should wrap non-Error values in an Error before calling trackException', () => {
    handler.handleError('something went wrong');
    expect(mockAppInsights.trackException).toHaveBeenCalledWith(jasmine.any(Error));
  });

  it('should wrap non-Error objects in an Error before calling trackException', () => {
    handler.handleError({ code: 42 });
    expect(mockAppInsights.trackException).toHaveBeenCalledWith(jasmine.any(Error));
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

```bash
cd web && ng test --include="**/global-error-handler.spec.ts" --watch=false
```
Expected: error — `GlobalErrorHandler` not found.

- [ ] **Step 3: Implement GlobalErrorHandler**

Create `web/src/app/global-error-handler.ts`:
```typescript
import { ErrorHandler, Injectable, inject } from '@angular/core';
import { AppInsightsService } from './services/app-insights.service';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly appInsights = inject(AppInsightsService);

  handleError(error: unknown): void {
    console.error(error);
    const err = error instanceof Error ? error : new Error(String(error));
    this.appInsights.trackException(err);
  }
}
```

- [ ] **Step 4: Run the spec**

```bash
cd web && ng test --include="**/global-error-handler.spec.ts" --watch=false
```
Expected: 4 specs, 0 failures.

- [ ] **Step 5: Register GlobalErrorHandler in app.config.ts**

Replace the full contents of `web/src/app/app.config.ts`:
```typescript
import { ApplicationConfig, ErrorHandler } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient } from '@angular/common/http';
import { GlobalErrorHandler } from './global-error-handler';

export const appConfig: ApplicationConfig = {
  providers: [
    provideAnimationsAsync(),
    provideHttpClient(),
    { provide: ErrorHandler, useClass: GlobalErrorHandler }
  ]
};
```

- [ ] **Step 6: Run full test suite to catch regressions**

```bash
cd web && ng test --watch=false
```
Expected: all existing tests pass plus 4 new ones.

- [ ] **Step 7: Commit**

```bash
git add web/src/app/global-error-handler.ts web/src/app/global-error-handler.spec.ts web/src/app/app.config.ts
git commit -m "feat(web): add GlobalErrorHandler wired to AppInsights and console"
```

---

## Task 5: Frontend — ErrorLoggingInterceptor

**Files:**
- Create: `web/src/app/interceptors/error-logging.interceptor.ts`
- Create: `web/src/app/interceptors/error-logging.interceptor.spec.ts`
- Modify: `web/src/app/app.config.ts`

- [ ] **Step 1: Write the failing spec**

Create `web/src/app/interceptors/error-logging.interceptor.spec.ts`:
```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { errorLoggingInterceptor } from './error-logging.interceptor';
import { AppInsightsService } from '../services/app-insights.service';

describe('errorLoggingInterceptor', () => {
  let httpMock: HttpTestingController;
  let http: HttpClient;
  let mockAppInsights: jasmine.SpyObj<AppInsightsService>;

  beforeEach(() => {
    mockAppInsights = jasmine.createSpyObj<AppInsightsService>('AppInsightsService', ['trackException']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorLoggingInterceptor])),
        provideHttpClientTesting(),
        { provide: AppInsightsService, useValue: mockAppInsights }
      ]
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);
    spyOn(console, 'error');
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should call console.error with url and status on HTTP error', () => {
    http.get('/api/test').subscribe({ error: () => {} });
    httpMock.expectOne('/api/test').flush('error', { status: 500, statusText: 'Server Error' });

    expect(console.error).toHaveBeenCalledWith(
      jasmine.stringContaining('500'),
      jasmine.stringContaining('/api/test'),
      jasmine.anything()
    );
  });

  it('should call trackException with url and status as custom properties on HTTP error', () => {
    http.get('/api/test').subscribe({ error: () => {} });
    httpMock.expectOne('/api/test').flush('error', { status: 503, statusText: 'Service Unavailable' });

    expect(mockAppInsights.trackException).toHaveBeenCalledWith(
      jasmine.any(Error),
      jasmine.objectContaining({ status: '503', url: jasmine.any(String) })
    );
  });

  it('should re-throw the error so the subscriber receives it', (done) => {
    http.get('/api/test').subscribe({
      error: (err) => {
        expect(err).toBeTruthy();
        done();
      }
    });
    httpMock.expectOne('/api/test').flush('error', { status: 404, statusText: 'Not Found' });
  });

  it('should pass through successful responses without logging', () => {
    http.get('/api/test').subscribe();
    httpMock.expectOne('/api/test').flush({ ok: true });

    expect(console.error).not.toHaveBeenCalled();
    expect(mockAppInsights.trackException).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

```bash
cd web && ng test --include="**/error-logging.interceptor.spec.ts" --watch=false
```
Expected: error — `errorLoggingInterceptor` not found.

- [ ] **Step 3: Implement the interceptor**

Create `web/src/app/interceptors/error-logging.interceptor.ts`:
```typescript
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AppInsightsService } from '../services/app-insights.service';

export const errorLoggingInterceptor: HttpInterceptorFn = (req, next) => {
  const appInsights = inject(AppInsightsService);
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        console.error(`HTTP ${err.status} on ${err.url}`, err.url, err);
        appInsights.trackException(
          new Error(`HTTP ${err.status}`),
          { url: err.url ?? '', status: String(err.status) }
        );
      }
      return throwError(() => err);
    })
  );
};
```

- [ ] **Step 4: Run the spec**

```bash
cd web && ng test --include="**/error-logging.interceptor.spec.ts" --watch=false
```
Expected: 4 specs, 0 failures.

- [ ] **Step 5: Register the interceptor in app.config.ts**

Replace the full contents of `web/src/app/app.config.ts`:
```typescript
import { ApplicationConfig, ErrorHandler } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { GlobalErrorHandler } from './global-error-handler';
import { errorLoggingInterceptor } from './interceptors/error-logging.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([errorLoggingInterceptor])),
    { provide: ErrorHandler, useClass: GlobalErrorHandler }
  ]
};
```

- [ ] **Step 6: Run the full test suite**

```bash
cd web && ng test --watch=false
```
Expected: all tests pass.

- [ ] **Step 7: Run all API tests**

```bash
cd api && dotnet test
```
Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
git add web/src/app/interceptors/error-logging.interceptor.ts web/src/app/interceptors/error-logging.interceptor.spec.ts web/src/app/app.config.ts
git commit -m "feat(web): add HTTP error logging interceptor wired to AppInsights and console"
```

---

## Task 6: Azure Portal — Set Daily Ingestion Cap

This is a one-time manual step in the Azure portal. No code changes.

- [ ] **Step 1: Open the Application Insights resource in the Azure portal**

Navigate to: Azure portal → Resource group → Application Insights resource used by the API.

- [ ] **Step 2: Set the daily cap**

Go to: **Configure** → **Usage and estimated costs** → **Daily cap** → **Set daily cap** → enter `0.1` GB → Save.

This prevents any runaway ingestion cost. If the cap fires daily in practice, investigate the error source or raise the cap — data dropped after the cap is gone permanently.
