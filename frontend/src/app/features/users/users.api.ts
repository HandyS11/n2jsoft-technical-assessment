import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { EMPTY, expand, mergeMap, Observable, toArray } from 'rxjs';
import { CursorPage } from '../../core/cursor-page';
import { PagedResult } from '../../core/paged-result';
import { SaveUser, UserDetails } from './user-details';
import { UserSummary } from './user-summary';

const usersUrl = '/api/v1/users';
/** The API's PageRequest.MaxPageSize: the fewest requests to read every active user. */
const largestPageSize = 100;

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  /** Every user when `isActive` is undefined, otherwise only the active or the inactive ones. */
  list(page: number, pageSize: number, isActive?: boolean): Observable<PagedResult<UserSummary>> {
    const params: Record<string, number | boolean> =
      isActive === undefined ? { page, pageSize } : { page, pageSize, isActive };
    return this.http.get<PagedResult<UserSummary>>(usersUrl, { params });
  }

  /** Every active user, in the API's order (last name, then first name), read 100 at a time. */
  listAllActive(): Observable<UserSummary[]> {
    return this.listActive().pipe(
      // Each slice starts after the last user read: users changing meanwhile cannot make it skip or repeat others.
      expand((slice) => (slice.next === null ? EMPTY : this.listActive(slice.next))),
      mergeMap((slice) => slice.items),
      toArray(),
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

  private listActive(after?: string): Observable<CursorPage<UserSummary>> {
    const params: Record<string, number | string> =
      after === undefined ? { pageSize: largestPageSize } : { pageSize: largestPageSize, after };
    return this.http.get<CursorPage<UserSummary>>(`${usersUrl}/active`, { params });
  }
}
