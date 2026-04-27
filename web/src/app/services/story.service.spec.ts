import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { StoryService } from './story.service';
import { StoriesResponse } from '../models/story.model';
import { environment } from '../../environments/environment';

describe('StoryService', () => {
  let service: StoryService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/api/stories`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [StoryService],
    });
    service = TestBed.inject(StoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should make a GET request to the correct URL with page and pageSize params', () => {
    service.getStories({ page: 1, pageSize: 10 }).subscribe();

    const req = httpMock.expectOne(
      r => r.url === baseUrl && r.params.get('page') === '1' && r.params.get('pageSize') === '10'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 1, pageSize: 10 } as StoriesResponse);
  });

  it('should include search param when provided', () => {
    service.getStories({ search: 'angular', page: 2, pageSize: 5 }).subscribe();

    const req = httpMock.expectOne(
      r =>
        r.url === baseUrl &&
        r.params.get('page') === '2' &&
        r.params.get('pageSize') === '5' &&
        r.params.get('search') === 'angular'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 2, pageSize: 5 } as StoriesResponse);
  });

  it('should omit search param when undefined', () => {
    service.getStories({ page: 1, pageSize: 20 }).subscribe();

    const req = httpMock.expectOne(
      r => r.url === baseUrl && !r.params.has('search')
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 1, pageSize: 20 } as StoriesResponse);
  });

  it('should return the StoriesResponse from the server', () => {
    const mockResponse: StoriesResponse = {
      items: [
        { id: 1, title: 'Test Story', url: 'https://example.com', by: 'user1', time: 1714000000, score: 100 },
      ],
      total: 1,
      page: 1,
      pageSize: 10,
    };

    let result: StoriesResponse | undefined;
    service.getStories({ page: 1, pageSize: 10 }).subscribe(res => (result = res));

    const req = httpMock.expectOne(
      r => r.url === baseUrl && r.params.get('page') === '1' && r.params.get('pageSize') === '10'
    );
    req.flush(mockResponse);

    expect(result).toEqual(mockResponse);
  });
});
