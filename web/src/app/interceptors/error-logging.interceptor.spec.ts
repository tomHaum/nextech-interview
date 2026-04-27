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
