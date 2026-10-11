import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { EMPTY, expand, Observable, reduce } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import { SaveUser, UserDetails } from './user-details';
import { UserSummary } from './user-summary';

const usersUrl = '/api/v1/users';
/** The API's PageRequest.MaxPageSize: the fewest requests to read every user. */
const largestPageSize = 100;

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  /** `isActive` lists only the active (true) or the inactive (false) users; omitted, every user. */
  list(page: number, pageSize: number, isActive?: boolean): Observable<PagedResult<UserSummary>> {
    const params = isActive === undefined ? { page, pageSize } : { page, pageSize, isActive };
    return this.http.get<PagedResult<UserSummary>>(usersUrl, { params });
  }

  /** Every active user, in the API's order (last name, then first name), read 100 at a time. */
  listAllActive(): Observable<UserSummary[]> {
    return this.list(1, largestPageSize, true).pipe(
      // Stops at an empty page too: users deleted meanwhile must not make it ask for pages forever.
      expand((page) =>
        page.items.length > 0 && page.page * page.pageSize < page.totalCount
          ? this.list(page.page + 1, largestPageSize, true)
          : EMPTY
      ),
      reduce((users: UserSummary[], page) => [...users, ...page.items], [])
    );
  }

  get(id: string): Observable<UserDetails> {
    return this.http.get<UserDetails>(`${usersUrl}/${id}`);
  }

  create(user: SaveUser): Observable<UserDetails> {
    return this.http.post<UserDetails>(usersUrl, user);
  }

  /** Refused with 412 when the user changed since `version` was read. */
  update(id: string, user: SaveUser, version: number): Observable<void> {
    return this.http.put<void>(`${usersUrl}/${id}`, user, {
      headers: { 'If-Match': `"${version}"` },
    });
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
