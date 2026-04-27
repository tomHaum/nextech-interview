import { ErrorHandler, Injectable, inject } from '@angular/core';
import { AppInsightsService } from './services/app-insights.service';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly appInsights = inject(AppInsightsService);

  handleError(error: unknown): void {
    console.error(error);
    const err = error instanceof Error ? error : new Error(String(error));
    this.appInsights.trackException(err);
  }
}
