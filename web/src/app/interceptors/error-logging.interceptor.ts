import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AppInsightsService } from '../services/app-insights.service';

export const errorLoggingInterceptor: HttpInterceptorFn = (req, next) => {
  const appInsights = inject(AppInsightsService);
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        console.error(`HTTP ${err.status} on ${err.url}`, err.url, err);
        appInsights.trackException(
          new Error(`HTTP ${err.status}`),
          { url: err.url ?? '', status: String(err.status) }
        );
      }
      return throwError(() => err);
    })
  );
};
