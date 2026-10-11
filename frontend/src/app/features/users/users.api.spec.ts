import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PagedResult } from '../../core/paged-result';
import { SaveUser, UserDetails } from './user-details';
import { UserSummary } from './user-summary';
import { UsersApi } from './users.api';

const id = '7d9f5f3c-2f1e-4a43-9a43-4d1f0c6a1b2e';
const jeanne: SaveUser = {
  firstName: 'Jeanne',
  lastName: 'Martin',
  address: { street: '5 rue Victor Hugo', postalCode: '33000', city: 'Bordeaux' },
  monthlyExpenseQuota: 8,
};
const details: UserDetails = { ...jeanne, id, isActive: true };

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

  it('lists a page of users with GET /api/v1/users', () => {
    const users: PagedResult<UserSummary> = {
      items: [
        {
          id,
          name: 'Juste Leblanc',
          city: 'Paris',
          monthlyExpenseQuota: 10,
          isActive: true,
        },
      ],
      page: 2,
      pageSize: 20,
      totalCount: 21,
    };
    let result: PagedResult<UserSummary> | undefined;

    api.list(2, 20).subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/users?page=2&pageSize=20');
    expect(request.request.method).toBe('GET');
    request.flush(users);
    expect(result).toEqual(users);
  });

  it('gets a user with GET /api/v1/users/:id', () => {
    let result: UserDetails | undefined;

    api.get(id).subscribe((value) => (result = value));

    const request = http.expectOne(`/api/v1/users/${id}`);
    expect(request.request.method).toBe('GET');
    request.flush(details);
    expect(result).toEqual(details);
  });

  it('creates a user with POST /api/v1/users', () => {
    let result: UserDetails | undefined;

    api.create(jeanne).subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/users');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(jeanne);
    request.flush(details, { status: 201, statusText: 'Created' });
    expect(result).toEqual(details);
  });

  it('updates a user with PUT /api/v1/users/:id', () => {
    api.update(id, jeanne).subscribe();

    const request = http.expectOne(`/api/v1/users/${id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(jeanne);
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('activates a user with POST /api/v1/users/:id/activate', () => {
    api.activate(id).subscribe();

    const request = http.expectOne(`/api/v1/users/${id}/activate`);
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('deactivates a user with POST /api/v1/users/:id/deactivate', () => {
    api.deactivate(id).subscribe();

    const request = http.expectOne(`/api/v1/users/${id}/deactivate`);
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('deletes a user with DELETE /api/v1/users/:id', () => {
    api.delete(id).subscribe();

    const request = http.expectOne(`/api/v1/users/${id}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });
});
