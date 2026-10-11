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
            get: () =>
              of({ id: '1', title: 'Juste Leblanc - Octobre 2025', userId: 'u1', year: 2025, month: 10 }),
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

  it.each([
    ['the users page', '/users', 'Utilisateurs'],
    ['the creation form', '/users/new', 'Nouvel utilisateur'],
    ['the edit form', '/users/1/edit', "Modifier l'utilisateur"],
    ['a report', '/expense-reports/1', 'Juste Leblanc - Octobre 2025'],
  ])('opens %s at %s', async (_page, url, heading) => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl(url);

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
      heading
    );
  });
});
