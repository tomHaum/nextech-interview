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
