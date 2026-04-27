import { Component } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { StoriesComponent } from './stories/stories.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [StoriesComponent, MatToolbarModule],
  styles: [`
    .hn-logo {
      width: 26px; height: 26px; background: white; color: var(--mat-sys-primary);
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
