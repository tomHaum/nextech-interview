# UI Refresh — HN-Inspired Visual Overhaul

**Date:** 2026-04-27
**Author:** Tom Haumersen (with AI partnering)
**Status:** Approved for implementation

## 1. Goal

Replace the default Angular Material skeleton with a polished HN-branded UI. Fix every spec gap from the original design doc (missing empty state, absolute timestamps, no "no link" label, spinner instead of progress bar, no autofocus) and add a top app bar to make the app feel like an intentional product.

Out of scope: dark mode, animations, routing changes, new pages.

## 2. Approach

**Custom Angular Material M3 theme + custom story row markup.**

- Define a custom M3 theme in `styles.scss` using `mat.$orange-palette` as primary so the toolbar, progress bar, paginator focus ring, and form field accents all pick up HN orange automatically.
- Remove the prebuilt `indigo-pink.css` import from `angular.json`.
- Keep `mat-toolbar`, `mat-progress-bar`, `mat-paginator`, and `mat-form-field` as real Material components (benefits from accessibility, keyboard nav, theme).
- Drop `mat-list`/`mat-list-item` — replace with a plain `<ul>` and custom SCSS for full styling control over density, left accent bar, and hover state.

## 3. Files Changed

| File | Change |
|---|---|
| `web/angular.json` | Remove prebuilt theme import |
| `web/src/styles.scss` | Custom M3 theme, global resets |
| `web/src/app/app.component.ts` | Add `mat-toolbar` shell |
| `web/src/app/stories/stories.component.html` | New layout (progress bar, custom list, empty state, no-link badge) |
| `web/src/app/stories/stories.component.scss` | New styles for custom story rows |
| `web/src/app/stories/stories.component.ts` | Wire `mat-progress-bar`, remove spinner import |
| `web/src/app/pipes/relative-time.pipe.ts` | **New** — pure pipe for relative timestamps |
| `web/src/app/pipes/relative-time.pipe.spec.ts` | **New** — unit tests for the pipe |
| `web/src/app/stories/stories.component.spec.ts` | Update for progress bar, empty state, relative time |
| `web/e2e/stories.spec.ts` | Update loading indicator selector |

## 4. Theme

In `styles.scss`, replace the prebuilt import with:

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
```

Remove from `angular.json` styles array:
```json
"@angular/material/prebuilt-themes/indigo-pink.css"
```

## 5. App Shell (`app.component.ts`)

Add `MatToolbarModule` to imports. Template:

```html
<mat-toolbar color="primary">
  <span class="hn-logo">Y</span>
  <span class="hn-title">Hacker News</span>
  <span class="hn-sub">/ newest</span>
</mat-toolbar>
<app-stories />
```

Toolbar logo/title styles go in `app.component.ts`'s `styles` array (scoped to the component):
- `.hn-logo`: `width:26px; height:26px; background:white; color:#ff6600; border-radius:4px; font-weight:900; font-size:14px; display:flex; align-items:center; justify-content:center`
- `.hn-title`: `font-weight:700; letter-spacing:-0.3px; margin-left:8px`
- `.hn-sub`: `opacity:0.7; font-size:13px; margin-left:4px`

## 6. Stories Component

### 6.1 Layout structure

```
<div class="hn-body">           ← grey page background
  <div class="hn-card">         ← white card, border-radius 8px, subtle shadow
    <mat-progress-bar>          ← indeterminate, shown while loading, hidden otherwise
    <mat-form-field>            ← search, autofocus
    <ul class="story-list">     ← custom rows
      <li class="story-row">
        <div class="accent-bar">
        <div class="story-content">
          <a | span>            ← link if url, span if not
          <span class="no-link-badge"> ← "no link", shown only when url is null
          <div class="story-meta"> ← "by X · 2h ago · N points"
    <div class="empty-state">   ← shown when total()===0 && !loading()
    <div class="error-state">   ← shown on error
    <div class="list-footer">   ← story count + mat-paginator
```

### 6.2 Story rows

Replace `mat-list` / `mat-list-item` with:

```html
<ul class="story-list">
  @for (story of stories(); track story.id) {
    <li class="story-row">
      <div class="accent-bar"></div>
      <div class="story-content">
        @if (story.url) {
          <a [href]="story.url" target="_blank" rel="noopener noreferrer" class="story-link">
            {{ story.title }}
          </a>
        } @else {
          <span class="story-title">
            {{ story.title }}
            <span class="no-link-badge">no link</span>
          </span>
        }
        <div class="story-meta">
          by {{ story.by }} &bull; {{ story.time | relativeTime }} &bull; {{ story.score }} points
        </div>
      </div>
    </li>
  }
</ul>
```

SCSS:
- `.story-list`: `list-style:none; margin:0; padding:0; transition:opacity 0.15s`
- `.story-list.loading`: `opacity:0.4; pointer-events:none`
- `.story-row`: `display:flex; gap:12px; padding:10px 16px; border-bottom:1px solid #f5f5f5`
- `.story-row:hover`: `background:#fffaf7`
- `.story-row:last-child`: `border-bottom:none`
- `.accent-bar`: `width:3px; border-radius:2px; background:#e8e8e8; flex-shrink:0; align-self:stretch`
- `.story-row:first-child .accent-bar`: `background:#ff6600` (orange on newest)
- `.story-link`: `font-size:14px; font-weight:600; color:#1a65b8; text-decoration:none`
- `.story-link:hover`: `text-decoration:underline`
- `.story-title`: `font-size:14px; font-weight:500; color:#444`
- `.no-link-badge`: `font-size:10px; background:#f0f0f0; color:#aaa; padding:1px 6px; border-radius:3px; margin-left:6px; vertical-align:middle`
- `.story-meta`: `font-size:12px; color:#999; margin-top:3px`

### 6.3 Loading state

Replace `mat-spinner` with `mat-progress-bar`:

```html
<mat-progress-bar mode="indeterminate" *ngIf="loading()"></mat-progress-bar>
```

Positioned at the top of `.hn-card` (full width, no border-radius on top edge). The search field and list render below it. List opacity reduced to 0.4 while loading via `[class.loading]="loading()"` on `.story-list`.

### 6.4 Empty state

Shown when `!loading() && !error() && total() === 0`:

```html
<div class="empty-state">
  <span class="empty-icon">🔍</span>
  <p class="empty-msg">
    @if (searchQuery) {
      No stories match <strong>"{{ searchQuery }}"</strong>
    } @else {
      No stories yet — check back in a moment
    }
  </p>
  @if (searchQuery) {
    <p class="empty-hint">Try a shorter or different search term</p>
  }
</div>
```

### 6.5 Error state

Keep existing logic. Update button to use `color="primary"` (`mat-stroked-button`) so it picks up orange from the theme.

### 6.6 Footer (count + paginator)

```html
<div class="list-footer">
  <span class="story-count">{{ total() }} {{ total() === 1 ? 'story' : 'stories' }}</span>
  <mat-paginator ...></mat-paginator>
</div>
```

`.list-footer`: `display:flex; align-items:center; border-top:1px solid #f0f0f0; padding:0 8px`
`.story-count`: `font-size:12px; color:#aaa; padding-left:8px; flex:1`

### 6.7 Search field autofocus

Add `cdkFocusInitial` directive to the search `<input matInput>`:

```html
<input matInput cdkFocusInitial [(ngModel)]="searchQuery" ... />
```

Import `A11yModule` (from `@angular/cdk/a11y`) in the component's `imports` array.

## 7. RelativeTimePipe

**New file:** `web/src/app/pipes/relative-time.pipe.ts`

Pure pipe. Input: Unix timestamp in seconds. Output: string.

| Elapsed | Output |
|---|---|
| < 60s | "just now" |
| < 60m | "Xm ago" |
| < 24h | "Xh ago" |
| < 7d | "Xd ago" |
| ≥ 7d | "Xw ago" |

Import in `StoriesComponent` imports array.

## 8. Testing

### 8.1 `RelativeTimePipe` spec (new)

Pure pipe — no TestBed needed. Cover all five time buckets plus boundary values (59s, 60s, 59m, 60m, 23h, 24h, 6d, 7d).

### 8.2 `StoriesComponent` spec (updates)

- Replace spinner selector (`mat-spinner`) with progress bar selector (`mat-progress-bar`) in loading test
- Add: empty state renders "No stories match" message when total is 0 and searchQuery is set
- Add: empty state renders no-search-term message when total is 0 and searchQuery is empty
- Add: `no-link-badge` is shown for stories with null URL
- Existing tests for link/plain-text rendering, search debounce, page reset remain valid

### 8.3 Playwright E2E (updates)

- Replace `mat-spinner` presence check with `mat-progress-bar` in any loading state test (currently no loading E2E test exists — no change needed unless one is added)

## 9. What is NOT changing

- API, services, models, routes — untouched
- Component logic (`StoryService`, `onSearch`, `onPage`, `load`) — untouched
- Existing passing tests — must continue to pass
