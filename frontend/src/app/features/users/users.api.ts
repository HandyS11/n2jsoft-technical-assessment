import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from './user-summary';

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>('/api/v1/users');
  }
}
