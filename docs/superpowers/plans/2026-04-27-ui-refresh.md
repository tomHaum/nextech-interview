# UI Refresh — HN-Inspired Theme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the default Angular Material skeleton with a polished HN-branded UI — custom orange M3 theme, top toolbar, custom story rows, relative timestamps, empty/loading/error states, and a "no link" badge.

**Architecture:** Custom Angular Material M3 theme drives the orange palette globally (toolbar, progress bar, paginator, form field accent). `mat-toolbar` added to the app shell. `mat-list` dropped in favour of a plain `<ul>` with full CSS control over density and left accent bar. New pure `RelativeTimePipe` converts Unix timestamps to "2h ago" strings. All existing component logic (`StoryService`, signals, paging, search debounce) is untouched.

**Tech Stack:** Angular 18, Angular Material 18 (M3 theming SCSS API), SCSS, Karma/Jasmine, Playwright

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `web/angular.json` | Modify | Remove prebuilt theme import |
| `web/src/styles.scss` | Modify | M3 custom theme + global resets |
| `web/src/app/app.component.ts` | Modify | Add `mat-toolbar` shell |
| `web/src/app/pipes/relative-time.pipe.ts` | **Create** | Pure pipe: Unix seconds → "2h ago" |
| `web/src/app/pipes/relative-time.pipe.spec.ts` | **Create** | Unit tests for the pipe |
| `web/src/app/stories/stories.component.ts` | Modify | Swap imports (spinner→progress-bar, drop mat-list/mat-card, add pipe) |
| `web/src/app/stories/stories.component.html` | Modify | Full rewrite: progress bar, custom rows, empty state, no-link badge |
| `web/src/app/stories/stories.component.scss` | Modify | Full rewrite: HN card, story rows, states |
| `web/src/app/stories/stories.component.spec.ts` | Modify | Update spinner→progress-bar test, update list-item selector, add empty-state + no-link-badge tests |

---

## Task 1: Custom Material Theme + App Toolbar

**Files:**
- Modify: `web/angular.json`
- Modify: `web/src/styles.scss`
- Modify: `web/src/app/app.component.ts`

- [ ] **Step 1: Remove the prebuilt theme from `angular.json`**

  Open `web/angular.json`. In `projects.web.architect.build.options.styles`, remove the prebuilt theme line. The array should look like:

  ```json
  "styles": [
    "src/styles.scss"
  ]
  ```

- [ ] **Step 2: Replace `styles.scss` with the custom M3 theme**

  Full content of `web/src/styles.scss`:

  ```scss
  @use '@angular/material' as mat;

  html {
    @include mat.theme((
      color: (
        primary: mat.$orange-palette,
        theme-type: light,
      ),
      typography: Roboto,
      density: 0,
    ));
  }

  html, body { height: 100%; }
  body { margin: 0; font-family: Roboto, "Helvetica Neue", sans-serif; }
  ```

- [ ] **Step 3: Add `mat-toolbar` to the app shell**

  Full content of `web/src/app/app.component.ts`:

  ```typescript
  import { Component } from '@angular/core';
  import { MatToolbarModule } from '@angular/material/toolbar';
  import { StoriesComponent } from './stories/stories.component';

  @Component({
    selector: 'app-root',
    standalone: true,
    imports: [StoriesComponent, MatToolbarModule],
    styles: [`
      .hn-logo {
        width: 26px; height: 26px; background: white; color: #ff6600;
        border-radius: 4px; font-weight: 900; font-size: 14px;
        display: flex; align-items: center; justify-content: center;
        flex-shrink: 0;
      }
      .hn-title { font-weight: 700; letter-spacing: -0.3px; margin-left: 8px; }
      .hn-sub { opacity: 0.7; font-size: 13px; margin-left: 4px; }
    `],
    template: `
      <mat-toolbar color="primary">
        <span class="hn-logo">Y</span>
        <span class="hn-title">Hacker News</span>
        <span class="hn-sub">/ newest</span>
      </mat-toolbar>
      <app-stories />
    `
  })
  export class AppComponent {}
  ```

- [ ] **Step 4: Serve and visually verify**

  ```bash
  cd web && npx ng serve
  ```

  Expected: orange toolbar at the top, stories card below on a light grey background. The paginator active page, form field focus ring, and progress spinner (still in use for now) should all be orange.

- [ ] **Step 5: Run existing unit tests — expect them to pass**

  ```bash
  cd web && npx ng test --watch=false --browsers=ChromeHeadless
  ```

  All 14 existing tests must still pass. If any fail, fix before proceeding.

- [ ] **Step 6: Commit**

  ```bash
  cd web && git add angular.json src/styles.scss src/app/app.component.ts
  git commit -m "feat(web): custom HN orange Material theme and top toolbar"
  ```

---

## Task 2: RelativeTimePipe (TDD)

**Files:**
- Create: `web/src/app/pipes/relative-time.pipe.spec.ts`
- Create: `web/src/app/pipes/relative-time.pipe.ts`

- [ ] **Step 1: Create the failing spec**

  Create `web/src/app/pipes/relative-time.pipe.spec.ts`:

  ```typescript
  import { RelativeTimePipe } from './relative-time.pipe';

  describe('RelativeTimePipe', () => {
    let pipe: RelativeTimePipe;
    const now = Math.floor(Date.now() / 1000);

    beforeEach(() => {
      pipe = new RelativeTimePipe();
    });

    it('returns "just now" for elapsed < 60s', () => {
      expect(pipe.transform(now - 0)).toBe('just now');
      expect(pipe.transform(now - 59)).toBe('just now');
    });

    it('returns "Xm ago" for elapsed 60s–3599s', () => {
      expect(pipe.transform(now - 60)).toBe('1m ago');
      expect(pipe.transform(now - 3599)).toBe('59m ago');
    });

    it('returns "Xh ago" for elapsed 1h–23h', () => {
      expect(pipe.transform(now - 3600)).toBe('1h ago');
      expect(pipe.transform(now - 86399)).toBe('23h ago');
    });

    it('returns "Xd ago" for elapsed 1d–6d', () => {
      expect(pipe.transform(now - 86400)).toBe('1d ago');
      expect(pipe.transform(now - 604799)).toBe('6d ago');
    });

    it('returns "Xw ago" for elapsed >= 7d', () => {
      expect(pipe.transform(now - 604800)).toBe('1w ago');
      expect(pipe.transform(now - 1209600)).toBe('2w ago');
    });
  });
  ```

- [ ] **Step 2: Run the spec — verify it fails**

  ```bash
  cd web && npx ng test --watch=false --browsers=ChromeHeadless --include="src/app/pipes/relative-time.pipe.spec.ts"
  ```

  Expected: FAIL — "Cannot find module './relative-time.pipe'"

- [ ] **Step 3: Create the pipe**

  Create `web/src/app/pipes/relative-time.pipe.ts`:

  ```typescript
  import { Pipe, PipeTransform } from '@angular/core';

  @Pipe({ name: 'relativeTime', standalone: true, pure: true })
  export class RelativeTimePipe implements PipeTransform {
    transform(unixSeconds: number): string {
      const elapsed = Math.floor(Date.now() / 1000) - unixSeconds;
      if (elapsed < 60) return 'just now';
      if (elapsed < 3600) return `${Math.floor(elapsed / 60)}m ago`;
      if (elapsed < 86400) return `${Math.floor(elapsed / 3600)}h ago`;
      if (elapsed < 604800) return `${Math.floor(elapsed / 86400)}d ago`;
      return `${Math.floor(elapsed / 604800)}w ago`;
    }
  }
  ```

- [ ] **Step 4: Run the spec — verify it passes**

  ```bash
  cd web && npx ng test --watch=false --browsers=ChromeHeadless --include="src/app/pipes/relative-time.pipe.spec.ts"
  ```

  Expected: 5 tests pass.

- [ ] **Step 5: Commit**

  ```bash
  cd web && git add src/app/pipes/relative-time.pipe.ts src/app/pipes/relative-time.pipe.spec.ts
  git commit -m "feat(web): add RelativeTimePipe for human-readable timestamps"
  ```

---

## Task 3: Stories Component Refactor

**Files:**
- Modify: `web/src/app/stories/stories.component.ts`
- Modify: `web/src/app/stories/stories.component.html`
- Modify: `web/src/app/stories/stories.component.scss`

- [ ] **Step 1: Update imports in `stories.component.ts`**

  Full content of `web/src/app/stories/stories.component.ts`:

  ```typescript
  import { Component, OnInit, OnDestroy, signal, inject } from '@angular/core';
  import { CommonModule } from '@angular/common';
  import { FormsModule } from '@angular/forms';
  import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
  import { MatInputModule } from '@angular/material/input';
  import { MatFormFieldModule } from '@angular/material/form-field';
  import { MatProgressBarModule } from '@angular/material/progress-bar';
  import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
  import { MatButtonModule } from '@angular/material/button';
  import { StoryService } from '../services/story.service';
  import { Story, StoriesResponse } from '../models/story.model';
  import { RelativeTimePipe } from '../pipes/relative-time.pipe';

  @Component({
    selector: 'app-stories',
    standalone: true,
    imports: [
      CommonModule, FormsModule,
      MatInputModule, MatFormFieldModule, MatProgressBarModule,
      MatPaginatorModule, MatButtonModule,
      RelativeTimePipe
    ],
    templateUrl: './stories.component.html',
    styleUrl: './stories.component.scss'
  })
  export class StoriesComponent implements OnInit, OnDestroy {
    private readonly storyService = inject(StoryService);
    private readonly destroy$ = new Subject<void>();
    private readonly searchSubject = new Subject<string>();

    stories = signal<Story[]>([]);
    total = signal(0);
    page = signal(1);
    pageSize = signal(20);
    loading = signal(false);
    error = signal(false);
    searchQuery = '';

    ngOnInit(): void {
      this.searchSubject.pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      ).subscribe(search => {
        this.page.set(1);
        this.load(search);
      });
      this.load();
    }

    ngOnDestroy(): void {
      this.destroy$.next();
      this.destroy$.complete();
    }

    onSearch(value: string): void {
      this.searchSubject.next(value);
    }

    onPage(event: PageEvent): void {
      this.page.set(event.pageIndex + 1);
      this.pageSize.set(event.pageSize);
      this.load(this.searchQuery);
    }

    load(search?: string): void {
      this.loading.set(true);
      this.storyService.getStories({
        search: search || undefined,
        page: this.page(),
        pageSize: this.pageSize()
      }).subscribe({
        next: (res: StoriesResponse) => {
          this.stories.set(res.items);
          this.total.set(res.total);
          this.loading.set(false);
          this.error.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.error.set(true);
        }
      });
    }
  }
  ```

- [ ] **Step 2: Rewrite `stories.component.html`**

  Full content of `web/src/app/stories/stories.component.html`:

  ```html
  <div class="hn-body">
    <div class="hn-card">

      <mat-progress-bar mode="indeterminate" *ngIf="loading()"></mat-progress-bar>

      <div class="search-wrap">
        <mat-form-field appearance="outline" class="search-field">
          <mat-label>Search stories</mat-label>
          <input matInput autofocus
                 [(ngModel)]="searchQuery"
                 (ngModelChange)="onSearch($event)"
                 placeholder="Filter by title..." />
        </mat-form-field>
      </div>

      <ng-container *ngIf="!error()">
        <ul class="story-list" [class.loading]="loading()">
          <li *ngFor="let story of stories()" class="story-row">
            <div class="accent-bar"></div>
            <div class="story-content">
              <a *ngIf="story.url"
                 [href]="story.url"
                 target="_blank"
                 rel="noopener noreferrer"
                 class="story-link">{{ story.title }}</a>
              <span *ngIf="!story.url" class="story-title">
                {{ story.title }}<span class="no-link-badge">no link</span>
              </span>
              <div class="story-meta">
                by {{ story.by }} &bull; {{ story.time | relativeTime }} &bull; {{ story.score }} points
              </div>
            </div>
          </li>
        </ul>

        <div *ngIf="!loading() && total() === 0" class="empty-state">
          <span class="empty-icon">🔍</span>
          <p class="empty-msg">
            <ng-container *ngIf="searchQuery">
              No stories match <strong>"{{ searchQuery }}"</strong>
            </ng-container>
            <ng-container *ngIf="!searchQuery">
              No stories yet — check back in a moment
            </ng-container>
          </p>
          <p *ngIf="searchQuery" class="empty-hint">Try a shorter or different search term</p>
        </div>
      </ng-container>

      <div *ngIf="error() && !loading()" class="error-container">
        <p class="error-message">Could not load stories. Check your connection.</p>
        <button mat-stroked-button color="primary" (click)="load()">↺&nbsp; Retry</button>
      </div>

      <div class="list-footer">
        <span *ngIf="!loading()" class="story-count">
          {{ total() }} {{ total() === 1 ? 'story' : 'stories' }}
        </span>
        <mat-paginator
          [length]="total()"
          [pageSize]="pageSize()"
          [pageIndex]="page() - 1"
          [pageSizeOptions]="[10, 20, 50]"
          (page)="onPage($event)"
          aria-label="Select page">
        </mat-paginator>
      </div>

    </div>
  </div>
  ```

- [ ] **Step 3: Rewrite `stories.component.scss`**

  Full content of `web/src/app/stories/stories.component.scss`:

  ```scss
  .hn-body {
    background: #f5f5f5;
    min-height: calc(100vh - 64px);
    padding: 20px;
    box-sizing: border-box;
  }

  .hn-card {
    max-width: 900px;
    margin: 0 auto;
    background: white;
    border-radius: 8px;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
    overflow: hidden;
  }

  .search-wrap {
    padding: 16px 16px 0;
  }

  .search-field {
    width: 100%;
  }

  // Story list
  .story-list {
    list-style: none;
    margin: 0;
    padding: 0;
    transition: opacity 0.15s;

    &.loading {
      opacity: 0.4;
      pointer-events: none;
    }
  }

  .story-row {
    display: flex;
    align-items: stretch;
    gap: 12px;
    padding: 10px 16px;
    border-bottom: 1px solid #f5f5f5;

    &:last-child {
      border-bottom: none;
    }

    &:hover {
      background: #fffaf7;
    }

    &:first-child .accent-bar {
      background: #ff6600;
    }
  }

  .accent-bar {
    width: 3px;
    border-radius: 2px;
    background: #e8e8e8;
    flex-shrink: 0;
    align-self: stretch;
  }

  .story-content {
    flex: 1;
    min-width: 0;
  }

  .story-link {
    display: block;
    font-size: 14px;
    font-weight: 600;
    color: #1a65b8;
    text-decoration: none;
    line-height: 1.35;

    &:hover {
      text-decoration: underline;
    }
  }

  .story-title {
    display: block;
    font-size: 14px;
    font-weight: 500;
    color: #444;
    line-height: 1.35;
  }

  .no-link-badge {
    display: inline-block;
    font-size: 10px;
    background: #f0f0f0;
    color: #aaa;
    padding: 1px 6px;
    border-radius: 3px;
    margin-left: 6px;
    vertical-align: middle;
    font-weight: 400;
  }

  .story-meta {
    font-size: 12px;
    color: #999;
    margin-top: 3px;
  }

  // Empty state
  .empty-state {
    padding: 48px 16px;
    text-align: center;
    color: #aaa;
  }

  .empty-icon {
    font-size: 32px;
    display: block;
    margin-bottom: 12px;
  }

  .empty-msg {
    font-size: 14px;
    margin: 0 0 6px;
    color: #888;
  }

  .empty-hint {
    font-size: 12px;
    color: #bbb;
    margin: 0;
  }

  // Error state
  .error-container {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 40px 16px;
    gap: 12px;
  }

  .error-message {
    color: #c62828;
    font-size: 14px;
    margin: 0;
  }

  // Footer
  .list-footer {
    display: flex;
    align-items: center;
    border-top: 1px solid #f0f0f0;
    padding: 0 8px;
  }

  .story-count {
    font-size: 12px;
    color: #aaa;
    padding-left: 8px;
    flex: 1;
  }
  ```

- [ ] **Step 4: Serve and do a quick visual check**

  ```bash
  cd web && npx ng serve
  ```

  Check: toolbar orange, stories render with left accent bar (first row orange), meta shows relative time, URL-less story shows "no link" badge, paginator shows story count on the left.

- [ ] **Step 5: Commit**

  ```bash
  cd web && git add src/app/stories/stories.component.ts \
    src/app/stories/stories.component.html \
    src/app/stories/stories.component.scss
  git commit -m "feat(web): refactor StoriesComponent with HN-styled layout and progress bar"
  ```

---

## Task 4: Update StoriesComponent Tests

**Files:**
- Modify: `web/src/app/stories/stories.component.spec.ts`

- [ ] **Step 1: Run the existing spec to see which tests now fail**

  ```bash
  cd web && npx ng test --watch=false --browsers=ChromeHeadless --include="src/app/stories/stories.component.spec.ts"
  ```

  Expected failures:
  - `should show loading spinner when loading is true` — `mat-spinner` no longer exists
  - `should hide spinner and display story titles after load` — `mat-list-item` no longer exists

- [ ] **Step 2: Replace the full spec with the updated version**

  Full content of `web/src/app/stories/stories.component.spec.ts`:

  ```typescript
  import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
  import { NoopAnimationsModule } from '@angular/platform-browser/animations';
  import { of } from 'rxjs';
  import { StoriesComponent } from './stories.component';
  import { StoryService } from '../services/story.service';
  import { Story, StoriesResponse } from '../models/story.model';

  const now = Math.floor(Date.now() / 1000);

  const mockStories: Story[] = [
    { id: 1, title: 'Angular 18 released', url: 'https://angular.io', by: 'user1', time: now - 7200, score: 100 },
    { id: 2, title: 'No URL story', url: null, by: 'user2', time: now - 3600, score: 50 },
  ];

  const mockResponse: StoriesResponse = {
    items: mockStories,
    total: 42,
    page: 1,
    pageSize: 20,
  };

  describe('StoriesComponent', () => {
    let fixture: ComponentFixture<StoriesComponent>;
    let component: StoriesComponent;
    let mockStoryService: jasmine.SpyObj<StoryService>;

    beforeEach(async () => {
      mockStoryService = jasmine.createSpyObj<StoryService>('StoryService', ['getStories']);
      mockStoryService.getStories.and.returnValue(of(mockResponse));

      await TestBed.configureTestingModule({
        imports: [StoriesComponent, NoopAnimationsModule],
        providers: [{ provide: StoryService, useValue: mockStoryService }],
      }).compileComponents();

      fixture = TestBed.createComponent(StoriesComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    });

    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should call getStories on init with page=1 and pageSize=20', () => {
      expect(mockStoryService.getStories).toHaveBeenCalledWith({
        search: undefined,
        page: 1,
        pageSize: 20,
      });
    });

    it('should show progress bar when loading is true', () => {
      component.loading.set(true);
      fixture.detectChanges();
      const bar = fixture.nativeElement.querySelector('mat-progress-bar');
      expect(bar).toBeTruthy();
    });

    it('should hide progress bar and display story rows after load', () => {
      component.loading.set(false);
      fixture.detectChanges();
      const bar = fixture.nativeElement.querySelector('mat-progress-bar');
      expect(bar).toBeNull();
      const rows = fixture.nativeElement.querySelectorAll('.story-row');
      expect(rows.length).toBe(2);
      const text = fixture.nativeElement.textContent;
      expect(text).toContain('Angular 18 released');
      expect(text).toContain('No URL story');
    });

    it('should render story with url as an anchor link', () => {
      component.loading.set(false);
      fixture.detectChanges();
      const links = fixture.nativeElement.querySelectorAll('a.story-link') as NodeListOf<HTMLAnchorElement>;
      const hrefs = Array.from(links).map(a => a.getAttribute('href'));
      expect(hrefs).toContain('https://angular.io');
    });

    it('should render story without url as plain text with no-link-badge', () => {
      component.loading.set(false);
      fixture.detectChanges();
      const badge = fixture.nativeElement.querySelector('.no-link-badge');
      expect(badge).toBeTruthy();
      expect(badge.textContent.trim()).toBe('no link');
      // The null-url story must not appear as a link
      const links = fixture.nativeElement.querySelectorAll('a.story-link') as NodeListOf<HTMLAnchorElement>;
      const hrefs = Array.from(links).map(a => a.getAttribute('href'));
      expect(hrefs).not.toContain(null as any);
    });

    it('should show empty state with search term when total is 0 and searchQuery is set', () => {
      component.loading.set(false);
      component.total.set(0);
      component.stories.set([]);
      component.searchQuery = 'xkcd';
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent;
      expect(text).toContain('No stories match');
      expect(text).toContain('xkcd');
      expect(text).toContain('Try a shorter or different search term');
    });

    it('should show fallback empty message when total is 0 and no search query', () => {
      component.loading.set(false);
      component.total.set(0);
      component.stories.set([]);
      component.searchQuery = '';
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent;
      expect(text).toContain('No stories yet');
    });

    it('should reset to page 1 on search', fakeAsync(() => {
      component.page.set(3);
      component.onSearch('angular');
      tick(300);
      fixture.detectChanges();
      expect(component.page()).toBe(1);
    }));

    it('should call getStories with search term after debounce', fakeAsync(() => {
      mockStoryService.getStories.calls.reset();
      component.onSearch('hacker');
      tick(300);
      fixture.detectChanges();
      expect(mockStoryService.getStories).toHaveBeenCalledWith({
        search: 'hacker',
        page: 1,
        pageSize: 20,
      });
    }));
  });
  ```

- [ ] **Step 3: Run the updated spec — all tests must pass**

  ```bash
  cd web && npx ng test --watch=false --browsers=ChromeHeadless --include="src/app/stories/stories.component.spec.ts"
  ```

  Expected: 9 tests pass (was 7 — added empty-state × 2, combined no-link + badge into one test).

- [ ] **Step 4: Commit**

  ```bash
  cd web && git add src/app/stories/stories.component.spec.ts
  git commit -m "test(web): update StoriesComponent spec for progress bar, empty state, and no-link badge"
  ```

---

## Task 5: Full Test Suite Verification

**Files:** none — verification only

- [ ] **Step 1: Run all Angular unit tests**

  ```bash
  cd web && npx ng test --watch=false --browsers=ChromeHeadless
  ```

  Expected: all tests pass. Count increases from 14 to ~21 — 2 new empty-state tests in the stories spec, 5 new pipe tests in relative-time.pipe.spec.

- [ ] **Step 2: Run Playwright E2E tests**

  ```bash
  cd web && npx playwright test
  ```

  Expected: all 7 E2E tests pass. The `mat-spinner` selector is not referenced in any E2E test, so no changes needed.

- [ ] **Step 3: Run API tests to confirm nothing regressed**

  ```bash
  cd api && dotnet test
  ```

  Expected: all 23 tests pass.

- [ ] **Step 4: Add the "rejected UI" entry to `docs/ai-prompts.md`**

  Append the following entry to `docs/ai-prompts.md`:

  ````markdown
  ---

  ## 8. Rejecting the initial UI design

  **Prompt (user):** "The UI looks pretty basic — can we do a full visual refresh?"

  **What AI initially produced:** The original design spec proposed: relative timestamps, a `(no link)` subtext on URL-less stories, a `mat-progress-bar` instead of a spinner, an empty state message, and autofocus on the search field. All reasonable additions — but the visual direction was still the default Angular Material skeleton (indigo-pink prebuilt theme, `mat-card` wrapper, `mat-list-item` rows). Functional, but generic.

  **Rejection:** The spec's visual direction was rejected as too close to the Material boilerplate. The reasoning: the challenge evaluates "elegance and structure of the design" — a default theme signals no intentionality.

  **What was built instead:** A full visual refresh using approach "1+3": a custom Angular Material M3 theme with HN orange (`mat.$orange-palette`) as the primary palette, a `mat-toolbar` app bar, and custom `<ul>/<li>` story rows (dropping `mat-list-item` for full CSS control). The result: HN-branded orange throughout, left accent bars, warm hover state, and a grey page background behind the white card.

  **Outcome:** The AI's spec gaps (empty state, relative time, no-link badge, progress bar) were all kept and implemented. The visual presentation was rebuilt from scratch with an intentional design direction rather than accepting the default skeleton.
  ````

- [ ] **Step 5: Commit the ai-prompts entry**

  ```bash
  git add docs/ai-prompts.md
  git commit -m "docs: add rejected UI design entry to ai-prompts log"
  ```
