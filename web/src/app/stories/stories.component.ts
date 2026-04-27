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
