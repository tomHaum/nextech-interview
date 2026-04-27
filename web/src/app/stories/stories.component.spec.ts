import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { StoriesComponent } from './stories.component';
import { StoryService } from '../services/story.service';
import { Story, StoriesResponse } from '../models/story.model';

const mockStories: Story[] = [
  { id: 1, title: 'Angular 18 released', url: 'https://angular.io', by: 'user1', time: 1700000000, score: 100 },
  { id: 2, title: 'No URL story', url: null, by: 'user2', time: 1700000100, score: 50 },
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

  it('should show loading spinner when loading is true', () => {
    component.loading.set(true);
    fixture.detectChanges();
    const spinner = fixture.nativeElement.querySelector('mat-spinner');
    expect(spinner).toBeTruthy();
  });

  it('should hide spinner and display story titles after load', () => {
    component.loading.set(false);
    fixture.detectChanges();
    const items = fixture.nativeElement.querySelectorAll('mat-list-item');
    expect(items.length).toBe(2);
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Angular 18 released');
    expect(text).toContain('No URL story');
  });

  it('should render story with url as an anchor link', () => {
    component.loading.set(false);
    fixture.detectChanges();
    const links = fixture.nativeElement.querySelectorAll('a');
    const hrefs = Array.from(links as NodeListOf<HTMLAnchorElement>).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('https://angular.io');
  });

  it('should render story without url as plain text (no link)', () => {
    component.loading.set(false);
    fixture.detectChanges();
    const links = fixture.nativeElement.querySelectorAll('a') as NodeListOf<HTMLAnchorElement>;
    const hrefs = Array.from(links).map((a) => a.getAttribute('href'));
    // The null-url story should not appear as a link
    expect(hrefs).not.toContain(null as any);
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('No URL story');
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
