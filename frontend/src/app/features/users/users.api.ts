import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import { SaveUser, UserDetails } from './user-details';
import { UserSummary } from './user-summary';

const usersUrl = '/api/v1/users';

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Observable<PagedResult<UserSummary>> {
    return this.http.get<PagedResult<UserSummary>>(usersUrl, {
      params: { page, pageSize },
    });
  }

  get(id: string): Observable<UserDetails> {
    return this.http.get<UserDetails>(`${usersUrl}/${id}`);
  }

  create(user: SaveUser): Observable<UserDetails> {
    return this.http.post<UserDetails>(usersUrl, user);
  }

  update(id: string, user: SaveUser): Observable<void> {
    return this.http.put<void>(`${usersUrl}/${id}`, user);
  }

  activate(id: string): Observable<void> {
    return this.http.post<void>(`${usersUrl}/${id}/activate`, null);
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${usersUrl}/${id}/deactivate`, null);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${usersUrl}/${id}`);
  }
}
