import { ApplicationConfig, ErrorHandler } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { GlobalErrorHandler } from './global-error-handler';
import { errorLoggingInterceptor } from './interceptors/error-logging.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([errorLoggingInterceptor])),
    { provide: ErrorHandler, useClass: GlobalErrorHandler }
  ]
};
