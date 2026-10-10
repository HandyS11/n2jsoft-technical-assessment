import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import { UserSummary } from './user-summary';

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(): Observable<PagedResult<UserSummary>> {
    return this.http.get<PagedResult<UserSummary>>('/api/v1/users');
  }
}
