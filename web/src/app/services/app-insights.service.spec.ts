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
