import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { UserSummary } from './user-summary';
import { UsersApi } from './users.api';

describe('UsersApi', () => {
  let api: UsersApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(UsersApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists users with GET /api/v1/users', () => {
    const users: UserSummary[] = [
      { id: '7d9f5f3c-2f1e-4a43-9a43-4d1f0c6a1b2e', name: 'Juste Leblanc' },
    ];
    let result: UserSummary[] | undefined;

    api.list().subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/users');
    expect(request.request.method).toBe('GET');
    request.flush(users);
    expect(result).toEqual(users);
  });
});
