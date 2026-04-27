import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AppInsightsService } from '../services/app-insights.service';

export const errorLoggingInterceptor: HttpInterceptorFn = (req, next) => {
  const appInsights = inject(AppInsightsService);
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        console.error(`HTTP ${err.status}`, req.url, err);
        appInsights.trackException(
          new Error(`HTTP ${err.status}`),
          { url: req.url, status: String(err.status) }
        );
      }
      return throwError(() => err);
    })
  );
};
