import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { appRoutes } from './app.routes';
import { ExpenseReportsApi } from './features/expense-reports/expense-reports.api';
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
            listAllActive: () => of([]),
            get: () =>
              of({
                id: '1',
                firstName: 'Juste',
                lastName: 'Leblanc',
                address: { street: '12 rue de Rivoli', postalCode: '75001', city: 'Paris' },
                monthlyExpenseQuota: 10,
                isActive: true,
              }),
          },
        },
        {
          provide: ExpenseReportsApi,
          useValue: {
            list: () => of({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
          },
        },
      ],
    });
  });

  it('opens the expense reports page at the root', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(TestBed.inject(Router).url).toBe('/expense-reports');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
      'Notes de frais'
    );
  });

  it('redirects an unknown URL to the expense reports page', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/page-inconnue');

    expect(TestBed.inject(Router).url).toBe('/expense-reports');
  });

  it('opens the users page at /users', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/users');

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
      'Utilisateurs'
    );
  });

  it('opens the creation form at /users/new', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/users/new');

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
      'Nouvel utilisateur'
    );
  });

  it('opens the edit form at /users/:id/edit', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/users/1/edit');

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
      "Modifier l'utilisateur"
    );
  });
});
