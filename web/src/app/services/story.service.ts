import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StoriesResponse } from '../models/story.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class StoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/stories`;

  getStories(params: { search?: string; page: number; pageSize: number }): Observable<StoriesResponse> {
    let httpParams = new HttpParams()
      .set('page', params.page)
      .set('pageSize', params.pageSize);
    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    return this.http.get<StoriesResponse>(this.baseUrl, { params: httpParams });
  }
}
