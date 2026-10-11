import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, Subject, throwError } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import { UserSummary } from '../users/user-summary';
import { UsersApi } from '../users/users.api';
import { ExpenseReportDetails } from './expense-report-details';
import ExpenseReportListPage from './expense-report-list.page';
import { ExpenseReportSummary } from './expense-report-summary';
import { ExpenseReportsApi } from './expense-reports.api';

const juste: UserSummary = {
  id: 'u1',
  name: 'Juste Leblanc',
  city: 'Paris',
  monthlyExpenseQuota: 10,
  isActive: true,
};
const marie: UserSummary = {
  id: 'u2',
  name: 'Marie Curie',
  city: 'Lyon',
  monthlyExpenseQuota: 5,
  isActive: true,
};
const october: ExpenseReportSummary = {
  id: '1',
  title: 'Juste Leblanc - Octobre 2025',
  year: 2025,
  month: 10,
};
const september: ExpenseReportSummary = {
  id: '2',
  title: 'Juste Leblanc - Septembre 2025',
  year: 2025,
  month: 9,
};
const monthMessage = 'Indiquez un mois au format AAAA-MM, entre 2000-01 et 2100-12.';
const inactiveUserMessage = 'Cet utilisateur est inactif : il ne peut pas être affecté à une note de frais.';

function createFails(status: number, code?: string): Observable<never> {
  return throwError(
    () => new HttpErrorResponse({ status, error: code === undefined ? null : { status, code } })
  );
}

function pageOf(
  items: ExpenseReportSummary[],
  page = 1,
  totalCount = items.length
): PagedResult<ExpenseReportSummary> {
  return { items, page, pageSize: 20, totalCount };
}

function fakeReportsApi(
  list: (page: number) => Observable<PagedResult<ExpenseReportSummary>> = () =>
    of(pageOf([october, september]))
) {
  return {
    list: vi.fn(list),
    create: vi.fn(
      (): Observable<ExpenseReportDetails> =>
        of({ id: '9', title: 'Juste Leblanc - Octobre 2025', userId: 'u1', year: 2025, month: 10 })
    ),
    delete: vi.fn((): Observable<void> => of(undefined)),
  };
}

function fakeUsersApi(listAllActive: () => Observable<UserSummary[]> = () => of([juste])) {
  return { listAllActive: vi.fn(listAllActive) };
}

async function render(reportsApi = fakeReportsApi(), usersApi = fakeUsersApi()) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'expense-reports', component: ExpenseReportListPage },
        { path: 'expense-reports/:id', children: [] },
      ]),
      { provide: ExpenseReportsApi, useValue: reportsApi },
      { provide: UsersApi, useValue: usersApi },
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/expense-reports', ExpenseReportListPage);
  harness.detectChanges();
  const element = () => harness.routeNativeElement as HTMLElement;
  const button = (label: string, rowIndex?: number) => {
    const scope =
      rowIndex === undefined ? element() : element().querySelectorAll('tbody tr')[rowIndex];
    const found = Array.from(scope.querySelectorAll('button')).find(
      (candidate) => candidate.textContent?.trim() === label
    );
    if (!found) {
      throw new Error(`No button "${label}"`);
    }
    return found;
  };

  return {
    element,
    button,
    detectChanges: () => harness.detectChanges(),
    text: () => element().textContent ?? '',
    rows: () =>
      Array.from(element().querySelectorAll('tbody tr')).map((row) =>
        row.querySelector('td')?.textContent?.trim()
      ),
    linkOf: (rowIndex: number) =>
      element().querySelectorAll('tbody tr')[rowIndex].querySelector('a')?.getAttribute('href'),
    alerts: () =>
      Array.from(element().querySelectorAll('[role="alert"]')).map((alert) =>
        alert.textContent?.trim()
      ),
    errors: () =>
      Array.from(element().querySelectorAll('.field-error')).map((error) =>
        error.textContent?.trim()
      ),
    options: () =>
      Array.from(element().querySelectorAll('#userId option')).map((option) =>
        option.textContent?.trim()
      ),
    chosenUser: () =>
      (element().querySelector('#userId') as HTMLSelectElement).selectedOptions[0]?.textContent?.trim(),
    month: () => element().querySelector('#month') as HTMLInputElement,
    click: (label: string, rowIndex?: number) => {
      button(label, rowIndex).click();
      harness.detectChanges();
    },
    choose: (userId: string) => {
      const select = element().querySelector('#userId') as HTMLSelectElement;
      select.value = userId;
      select.dispatchEvent(new Event('change'));
      harness.detectChanges();
    },
    fillMonth: (value: string) => {
      const input = element().querySelector('#month') as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      harness.detectChanges();
    },
    submit: async () => {
      element().querySelector('form')?.dispatchEvent(new Event('submit'));
      harness.detectChanges();
      await harness.fixture.whenStable();
    },
  };
}

describe('ExpenseReportListPage', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  describe('list', () => {
    it('shows each report title, linked to its page', async () => {
      const page = await render();

      expect(page.rows()).toEqual(['Juste Leblanc - Octobre 2025', 'Juste Leblanc - Septembre 2025']);
      expect(page.linkOf(0)).toBe('/expense-reports/1');
    });

    it('asks the API for the first page of 20 reports', async () => {
      const reportsApi = fakeReportsApi();

      await render(reportsApi);

      expect(reportsApi.list).toHaveBeenCalledWith(1, 20);
    });

    it('says so when there is no report', async () => {
      const page = await render(fakeReportsApi(() => of(pageOf([]))));

      expect(page.element().querySelector('table')).toBeNull();
      expect(page.text()).toContain('Aucune note de frais.');
    });

    it('says so when the reports cannot be loaded', async () => {
      const page = await render(
        fakeReportsApi(() => throwError(() => new Error('Service unavailable')))
      );

      expect(page.alerts()).toContain('Impossible de charger les notes de frais.');
    });

    it('pages through the reports', async () => {
      const reportsApi = fakeReportsApi((pageNumber) =>
        of(pageOf(pageNumber === 1 ? [october] : [september], pageNumber, 45))
      );
      const page = await render(reportsApi);
      expect(page.text()).toContain('Page 1 sur 3');

      page.click('Suivant');

      expect(reportsApi.list).toHaveBeenLastCalledWith(2, 20);
      expect(page.rows()).toEqual(['Juste Leblanc - Septembre 2025']);
      expect(page.text()).toContain('Page 2 sur 3');
    });

    it('deletes a report once the deletion is confirmed, then reloads the page', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const reportsApi = fakeReportsApi();
      const page = await render(reportsApi);

      page.click('Supprimer', 0);

      expect(confirm).toHaveBeenCalledWith(
        'Supprimer la note de frais « Juste Leblanc - Octobre 2025 » ? Ses dépenses seront supprimées avec elle.'
      );
      expect(reportsApi.delete).toHaveBeenCalledWith('1');
      expect(reportsApi.list).toHaveBeenCalledTimes(2);
    });

    it('keeps the report when the deletion is cancelled', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const reportsApi = fakeReportsApi();
      const page = await render(reportsApi);

      page.click('Supprimer', 0);

      expect(reportsApi.delete).not.toHaveBeenCalled();
    });

    it('goes back one page when the last report of the last page is deleted', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      let deleted = false;
      const reportsApi = fakeReportsApi((pageNumber) => {
        if (pageNumber === 1) {
          return of(pageOf([october], 1, deleted ? 20 : 21));
        }
        return of(deleted ? pageOf([], 2, 20) : pageOf([september], 2, 21));
      });
      reportsApi.delete.mockImplementation(() => {
        deleted = true;
        return of(undefined);
      });
      const page = await render(reportsApi);
      page.click('Suivant');

      page.click('Supprimer', 0);

      expect(page.rows()).toEqual(['Juste Leblanc - Octobre 2025']);
      expect(page.text()).toContain('Page 1 sur 1');
    });

    it('says so when a deletion fails, and reloads the page', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const reportsApi = fakeReportsApi();
      reportsApi.delete.mockImplementation(() =>
        throwError(() => new HttpErrorResponse({ status: 500 }))
      );
      const page = await render(reportsApi);

      page.click('Supprimer', 0);

      expect(page.alerts()).toContain("L'opération a échoué. Veuillez réessayer.");
      expect(reportsApi.list).toHaveBeenCalledTimes(2);
    });
  });

  describe('creation form', () => {
    it('starts at the current month', async () => {
      vi.useFakeTimers({ toFake: ['Date'] });
      vi.setSystemTime(new Date(2026, 9, 11));

      const page = await render();

      expect(page.month().value).toBe('2026-10');
    });

    it('offers the active users', async () => {
      const usersApi = fakeUsersApi();

      const page = await render(fakeReportsApi(), usersApi);

      expect(usersApi.listAllActive).toHaveBeenCalled();
      expect(page.options()).toEqual(['Choisissez un utilisateur', 'Juste Leblanc']);
    });

    it('says so when there is no active user', async () => {
      const page = await render(fakeReportsApi(), fakeUsersApi(() => of([])));

      expect(page.text()).toContain('Aucun utilisateur actif.');
      expect(page.element().querySelector('form')).toBeNull();
    });

    it('says so when the users cannot be loaded', async () => {
      const page = await render(
        fakeReportsApi(),
        fakeUsersApi(() => throwError(() => new Error('Service unavailable')))
      );

      expect(page.alerts()).toContain('Impossible de charger les utilisateurs.');
      expect(page.element().querySelector('form')).toBeNull();
    });

    it('asks for a user instead of creating', async () => {
      const reportsApi = fakeReportsApi();
      const page = await render(reportsApi);

      await page.submit();

      expect(reportsApi.create).not.toHaveBeenCalled();
      expect(page.errors()).toEqual(['Choisissez un utilisateur.']);
    });

    it.each(['', '1999-12', '2101-01'])('refuses the month "%s"', async (month) => {
      const reportsApi = fakeReportsApi();
      const page = await render(reportsApi);
      page.choose('u1');
      page.fillMonth(month);

      await page.submit();

      expect(reportsApi.create).not.toHaveBeenCalled();
      expect(page.errors()).toEqual([monthMessage]);
    });

    it('creates the report for the chosen user and month, then opens it', async () => {
      const reportsApi = fakeReportsApi();
      const page = await render(reportsApi);
      page.choose('u1');
      page.fillMonth('2025-10');

      await page.submit();

      expect(reportsApi.create).toHaveBeenCalledWith({ userId: 'u1', year: 2025, month: 10 });
      expect(TestBed.inject(Router).url).toBe('/expense-reports/9');
    });

    it.each([
      {
        status: 422,
        body: { status: 422, code: 'expense_report.inactive_user' },
        message: 'Cet utilisateur est inactif : il ne peut pas être affecté à une note de frais.',
      },
      {
        status: 422,
        body: { status: 422, code: 'expense_report.already_exists' },
        message: 'Une note de frais existe déjà pour cet utilisateur et ce mois.',
      },
      { status: 404, body: null, message: 'Utilisateur introuvable.' },
      { status: 500, body: null, message: "L'opération a échoué. Veuillez réessayer." },
    ])('says "$message" when the API answers $status', async ({ status, body, message }) => {
      const reportsApi = fakeReportsApi();
      reportsApi.create.mockImplementation(() =>
        throwError(() => new HttpErrorResponse({ status, error: body }))
      );
      const page = await render(reportsApi);
      page.choose('u1');

      await page.submit();

      expect(page.alerts()).toContain(message);
      expect(page.button('Créer').disabled).toBe(false);
      expect(TestBed.inject(Router).url).toBe('/expense-reports');
    });

    it.each([
      { status: 422, code: 'expense_report.inactive_user' },
      { status: 404, code: undefined },
    ])('reloads the active users when the API answers $status $code', async ({ status, code }) => {
      const reportsApi = fakeReportsApi();
      reportsApi.create.mockImplementation(() => createFails(status, code));
      const usersApi = fakeUsersApi();
      const page = await render(reportsApi, usersApi);
      usersApi.listAllActive.mockImplementation(() => of([marie]));
      page.choose('u1');

      await page.submit();

      expect(usersApi.listAllActive).toHaveBeenCalledTimes(2);
      expect(page.options()).toEqual(['Choisissez un utilisateur', 'Marie Curie']);
    });

    it.each([
      { status: 422, code: 'expense_report.already_exists' },
      { status: 500, code: undefined },
    ])('keeps the active users when the API answers $status $code', async ({ status, code }) => {
      const reportsApi = fakeReportsApi();
      reportsApi.create.mockImplementation(() => createFails(status, code));
      const usersApi = fakeUsersApi();
      const page = await render(reportsApi, usersApi);
      page.choose('u1');

      await page.submit();

      expect(usersApi.listAllActive).toHaveBeenCalledTimes(1);
    });

    it('keeps the users and the error shown while the users reload', async () => {
      const reportsApi = fakeReportsApi();
      reportsApi.create.mockImplementation(() => createFails(422, 'expense_report.inactive_user'));
      const usersApi = fakeUsersApi();
      const page = await render(reportsApi, usersApi);
      usersApi.listAllActive.mockImplementation(() => new Subject<UserSummary[]>());
      page.choose('u1');

      await page.submit();

      expect(page.options()).toEqual(['Choisissez un utilisateur', 'Juste Leblanc']);
      expect(page.alerts()).toContain(inactiveUserMessage);
    });

    it('clears the chosen user once the reloaded users no longer include them', async () => {
      const reportsApi = fakeReportsApi();
      reportsApi.create.mockImplementation(() => createFails(422, 'expense_report.inactive_user'));
      const usersApi = fakeUsersApi(() => of([juste, marie]));
      const page = await render(reportsApi, usersApi);
      usersApi.listAllActive.mockImplementation(() => of([marie]));
      page.choose('u1');

      await page.submit();

      expect(page.chosenUser()).toBe('Choisissez un utilisateur');
      expect(page.alerts()).toContain(inactiveUserMessage);
      await page.submit();
      expect(reportsApi.create).toHaveBeenCalledTimes(1);
      expect(page.errors()).toEqual(['Choisissez un utilisateur.']);
    });

    it('keeps the chosen user when the reloaded users still include them', async () => {
      const reportsApi = fakeReportsApi();
      reportsApi.create.mockImplementation(() => createFails(404));
      const usersApi = fakeUsersApi(() => of([juste, marie]));
      const page = await render(reportsApi, usersApi);
      usersApi.listAllActive.mockImplementation(() => of([marie]));
      page.choose('u2');

      await page.submit();

      expect(page.chosenUser()).toBe('Marie Curie');
    });

    it('ignores users arriving after a later reload', async () => {
      const reportsApi = fakeReportsApi();
      reportsApi.create.mockImplementation(() => createFails(422, 'expense_report.inactive_user'));
      const usersApi = fakeUsersApi(() => of([juste, marie]));
      const page = await render(reportsApi, usersApi);
      const firstReload = new Subject<UserSummary[]>();
      usersApi.listAllActive.mockImplementationOnce(() => firstReload).mockImplementationOnce(() => of([marie]));
      page.choose('u1');
      await page.submit();
      page.choose('u2');
      // The first reload is still running when the second failure reloads the users again, which answers at once.
      await page.submit();

      firstReload.next([juste, marie]);
      page.detectChanges();

      expect(page.options()).toEqual(['Choisissez un utilisateur', 'Marie Curie']);
    });
  });
});
