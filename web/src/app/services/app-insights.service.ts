import { Injectable } from '@angular/core';
import { ApplicationInsights } from '@microsoft/applicationinsights-web';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AppInsightsService {
  readonly appInsights: ApplicationInsights | null;

  constructor() {
    if (!environment.appInsightsConnectionString) {
      this.appInsights = null;
      return;
    }
    this.appInsights = new ApplicationInsights({
      config: { connectionString: environment.appInsightsConnectionString }
    });
    this.appInsights.loadAppInsights();
  }

  trackException(error: Error, properties?: { [key: string]: string }): void {
    this.appInsights?.trackException({ exception: error }, properties);
  }
}
