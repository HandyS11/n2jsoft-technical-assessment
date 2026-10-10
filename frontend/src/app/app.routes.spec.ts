import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { appRoutes } from './app.routes';
import { UsersApi } from './features/users/users.api';

describe('appRoutes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(appRoutes),
        {
          provide: UsersApi,
          useValue: {
            list: () => of({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
          },
        },
      ],
    });
  });

  it('opens the users page at the root', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(TestBed.inject(Router).url).toBe('/users');
  });

  it('redirects an unknown URL to the users page', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/page-inconnue');

    expect(TestBed.inject(Router).url).toBe('/users');
  });
});
