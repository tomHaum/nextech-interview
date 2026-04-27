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
