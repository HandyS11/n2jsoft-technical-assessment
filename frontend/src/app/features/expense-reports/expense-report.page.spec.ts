import { registerLocaleData } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import localeFr from '@angular/common/locales/fr';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, Subject, throwError } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import { ExpenseDetails, ExpenseSummary } from '../expenses/expense';
import { ExpensesApi } from '../expenses/expenses.api';
import { ExpenseReportDetails } from './expense-report-details';
import ExpenseReportPage from './expense-report.page';
import { ExpenseReportsApi } from './expense-reports.api';

registerLocaleData(localeFr);

const october: ExpenseReportDetails = {
  id: '1',
  title: 'Juste Leblanc - Octobre 2025',
  userId: 'u1',
  year: 2025,
  month: 10,
  expenseCount: 3,
  total: 1234.5,
  monthlyExpenseQuota: 10,
  isReadOnly: false,
};

const taxi: ExpenseSummary = {
  id: 'e1',
  date: '2025-10-15',
  description: 'Taxi',
  amount: 23.5,
  billingAddress: { storeName: 'G7', street: '12 rue de Rivoli', postalCode: '75001', city: 'Paris' },
};
const taxiDetails: ExpenseDetails = { ...taxi, version: 7 };
const train: ExpenseSummary = { ...taxi, id: 'e2', description: 'Train' };
const trainDetails: ExpenseDetails = { ...train, version: 3 };

function expensePage(items: ExpenseSummary[], page = 1, totalCount = items.length): PagedResult<ExpenseSummary> {
  return { items, page, pageSize: 5, totalCount };
}

function httpError(status: number): Observable<never> {
  return throwError(() => new HttpErrorResponse({ status }));
}

function fakeApi() {
  return {
    get: vi.fn((): Observable<ExpenseReportDetails> => of(october)),
    delete: vi.fn((): Observable<void> => of(undefined)),
  };
}

function fakeExpensesApi() {
  return {
    list: vi.fn((_reportId: string, page: number): Observable<PagedResult<ExpenseSummary>> =>
      of(expensePage([taxi], page))
    ),
    get: vi.fn<(reportId: string, id: string) => Observable<ExpenseDetails>>(() => of(taxiDetails)),
    create: vi.fn((): Observable<ExpenseDetails> => of(taxiDetails)),
    update: vi.fn((): Observable<void> => of(undefined)),
    delete: vi.fn((): Observable<void> => of(undefined)),
  };
}

/** Collapses the narrow and non-breaking spaces of fr-FR numbers ("1 234,50 €") into plain spaces. */
function text(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

async function render(api = fakeApi(), expensesApi = fakeExpensesApi()) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'expense-reports', children: [] },
        { path: 'expense-reports/:id', component: ExpenseReportPage },
      ]),
      { provide: ExpenseReportsApi, useValue: api },
      { provide: ExpensesApi, useValue: expensesApi },
      { provide: LOCALE_ID, useValue: 'fr-FR' },
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/expense-reports/1', ExpenseReportPage);
  harness.detectChanges();
  const element = () => harness.routeNativeElement as HTMLElement;
  const button = (name: string, scope: ParentNode = element()) =>
    Array.from(scope.querySelectorAll('button')).find(
      (candidate) => candidate.textContent?.trim() === name
    ) as HTMLButtonElement | undefined;
  const rows = () => Array.from(element().querySelectorAll('tbody tr'));
  const click = async (target: HTMLButtonElement | undefined) => {
    target?.click();
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  return {
    element,
    button,
    rows,
    click,
    harness,
    deleteButton: () => button('Supprimer la note de frais') as HTMLButtonElement,
    heading: () => element().querySelector('h1')?.textContent?.trim(),
    alert: () => element().querySelector('[role="alert"]')?.textContent?.trim(),
    status: () => element().querySelector('[role="status"]')?.textContent?.trim(),
    cells: (row: Element) => Array.from(row.querySelectorAll('td')).map((cell) => text(cell)),
    form: () => element().querySelector('app-expense-form'),
    remove: async () => {
      button('Supprimer la note de frais')?.click();
      harness.detectChanges();
      await harness.fixture.whenStable();
    },
  };
}

describe('ExpenseReportPage', () => {
  afterEach(() => vi.restoreAllMocks());

  describe('header', () => {
    it('shows the title and the month of the report', async () => {
      const api = fakeApi();

      const page = await render(api);

      expect(api.get).toHaveBeenCalledWith('1');
      expect(page.heading()).toBe('Juste Leblanc - Octobre 2025');
      expect(page.element().textContent).toContain('Mois : Octobre 2025');
    });

    it('shows how many expenses the quota allows, and their total', async () => {
      const page = await render();

      expect(text(page.element())).toContain('3 dépenses sur 10 ce mois-ci');
      expect(text(page.element())).toContain('Total : 1 234,50 €');
    });

    it('writes a single expense in the singular', async () => {
      const api = fakeApi();
      api.get.mockImplementation(() => of({ ...october, expenseCount: 1 }));

      const page = await render(api);

      expect(text(page.element())).toContain('1 dépense sur 10 ce mois-ci');
    });

    it('links back to the list', async () => {
      const page = await render();

      const back = page.element().querySelector('a');
      expect(back?.textContent?.trim()).toBe('← Notes de frais');
      expect(back?.getAttribute('href')).toBe('/expense-reports');
    });

    it('links to the user of the report', async () => {
      const page = await render();

      const link = Array.from(page.element().querySelectorAll('a')).find(
        (candidate) => candidate.textContent?.trim() === "Voir l'utilisateur"
      );
      expect(link?.getAttribute('href')).toBe('/users/u1/edit');
    });

    it('does not link to the user once deleted', async () => {
      const api = fakeApi();
      api.get.mockImplementation(() => of({ ...october, isReadOnly: true }));

      const page = await render(api);

      expect(page.element().textContent).not.toContain("Voir l'utilisateur");
    });

    it('says so when the report does not exist', async () => {
      const api = fakeApi();
      api.get.mockImplementation(() => httpError(404));

      const page = await render(api);

      expect(page.alert()).toBe('Note de frais introuvable.');
      expect(page.heading()).toBeUndefined();
    });

    it('says so when the report cannot be loaded', async () => {
      const api = fakeApi();
      api.get.mockImplementation(() => httpError(500));

      const page = await render(api);

      expect(page.alert()).toBe('Impossible de charger la note de frais.');
    });

    it('shows the latest read of the report, even when an earlier one answers last', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      const page = await render(api);
      const reads = [new Subject<ExpenseReportDetails>(), new Subject<ExpenseReportDetails>()];
      api.get.mockImplementationOnce(() => reads[0]).mockImplementationOnce(() => reads[1]);
      await page.click(page.button('Supprimer', page.rows()[0]));
      await page.click(page.button('Supprimer', page.rows()[0]));

      reads[1].next({ ...october, expenseCount: 1 });
      reads[0].next({ ...october, expenseCount: 2 });
      page.harness.detectChanges();

      expect(text(page.element())).toContain('1 dépense sur 10 ce mois-ci');
    });

    it('drops the load error once the report is read again', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      const page = await render(api);
      api.get.mockImplementationOnce(() => httpError(500));
      await page.click(page.button('Supprimer', page.rows()[0]));
      expect(page.alert()).toBe('Impossible de charger la note de frais.');

      await page.click(page.button('Supprimer', page.rows()[0]));

      expect(page.alert()).toBeUndefined();
      expect(page.heading()).toBe('Juste Leblanc - Octobre 2025');
    });
  });

  describe('report deletion', () => {
    it('deletes the report once confirmed, then goes back to the list', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      const page = await render(api);

      await page.remove();

      expect(confirm).toHaveBeenCalledWith(
        'Supprimer la note de frais « Juste Leblanc - Octobre 2025 » ? Ses dépenses seront supprimées avec elle.'
      );
      expect(api.delete).toHaveBeenCalledWith('1');
      expect(TestBed.inject(Router).url).toBe('/expense-reports');
    });

    it('keeps the report when the deletion is cancelled', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const api = fakeApi();
      const page = await render(api);

      await page.remove();

      expect(api.delete).not.toHaveBeenCalled();
      expect(TestBed.inject(Router).url).toBe('/expense-reports/1');
    });

    it('goes back to the list when the report was already deleted', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      api.delete.mockImplementation(() => httpError(404));
      const page = await render(api);

      await page.remove();

      expect(TestBed.inject(Router).url).toBe('/expense-reports');
    });

    it('says so when the deletion fails', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      api.delete.mockImplementation(() => httpError(500));
      const page = await render(api);

      await page.remove();

      expect(page.alert()).toBe('La suppression a échoué. Veuillez réessayer.');
      expect(page.deleteButton().disabled).toBe(false);
      expect(TestBed.inject(Router).url).toBe('/expense-reports/1');
    });
  });

  describe('expenses', () => {
    it('lists the first 5 expenses of the report', async () => {
      const expensesApi = fakeExpensesApi();

      const page = await render(fakeApi(), expensesApi);

      expect(expensesApi.list).toHaveBeenCalledWith('1', 1, 5);
      expect(page.cells(page.rows()[0])).toEqual([
        'mercredi 15 octobre 2025',
        'Taxi',
        '23,50 €',
        'G7, 12 rue de Rivoli, 75001 Paris',
        'Modifier Supprimer',
      ]);
    });

    it('says so when the report has no expense', async () => {
      const expensesApi = fakeExpensesApi();
      expensesApi.list.mockImplementation(() => of(expensePage([])));

      const page = await render(fakeApi(), expensesApi);

      expect(page.element().textContent).toContain('Aucune dépense.');
    });

    it('says so when the expenses cannot be loaded', async () => {
      const expensesApi = fakeExpensesApi();
      expensesApi.list.mockImplementation(() => httpError(500));

      const page = await render(fakeApi(), expensesApi);

      expect(page.alert()).toBe('Impossible de charger les dépenses.');
    });

    it('pages through the expenses 5 at a time', async () => {
      const expensesApi = fakeExpensesApi();
      expensesApi.list.mockImplementation((_reportId, pageNumber) => of(expensePage([taxi], pageNumber, 6)));
      const page = await render(fakeApi(), expensesApi);
      expect(page.element().textContent).toContain('Page 1 sur 2');

      await page.click(page.button('Suivant'));

      expect(expensesApi.list).toHaveBeenLastCalledWith('1', 2, 5);
      expect(page.element().textContent).toContain('Page 2 sur 2');
    });

    it('offers the form to add an expense', async () => {
      const page = await render();

      expect(page.form()).not.toBeNull();
      expect(text(page.form()?.querySelector('h2'))).toBe('Nouvelle dépense');
    });

    it('loads an expense into the form to edit it', async () => {
      const expensesApi = fakeExpensesApi();
      const page = await render(fakeApi(), expensesApi);

      await page.click(page.button('Modifier', page.rows()[0]));

      expect(expensesApi.get).toHaveBeenCalledWith('1', 'e1');
      expect(text(page.form()?.querySelector('h2'))).toBe('Modifier la dépense');
      expect((page.form()?.querySelector('#description') as HTMLInputElement).value).toBe('Taxi');
    });

    it('edits the last expense asked for, even when an earlier one answers last', async () => {
      const expensesApi = fakeExpensesApi();
      expensesApi.list.mockImplementation(() => of(expensePage([taxi, train])));
      const loads = new Map([
        ['e1', new Subject<ExpenseDetails>()],
        ['e2', new Subject<ExpenseDetails>()],
      ]);
      expensesApi.get.mockImplementation((_reportId: string, id: string) => loads.get(id) as Subject<ExpenseDetails>);
      const page = await render(fakeApi(), expensesApi);
      await page.click(page.button('Modifier', page.rows()[0]));
      await page.click(page.button('Modifier', page.rows()[1]));

      loads.get('e2')?.next(trainDetails);
      loads.get('e1')?.next(taxiDetails);
      page.harness.detectChanges();

      expect((page.form()?.querySelector('#description') as HTMLInputElement).value).toBe('Train');
    });

    it('says so when the expense to edit was deleted meanwhile, and reloads the list', async () => {
      const expensesApi = fakeExpensesApi();
      expensesApi.get.mockImplementation(() => httpError(404));
      const page = await render(fakeApi(), expensesApi);

      await page.click(page.button('Modifier', page.rows()[0]));

      expect(page.status()).toBe('Dépense introuvable.');
      expect(expensesApi.list).toHaveBeenCalledTimes(2);
      expect(text(page.form()?.querySelector('h2'))).toBe('Nouvelle dépense');
    });

    it('says so when the expense to edit cannot be loaded', async () => {
      const expensesApi = fakeExpensesApi();
      expensesApi.get.mockImplementation(() => httpError(500));
      const page = await render(fakeApi(), expensesApi);

      await page.click(page.button('Modifier', page.rows()[0]));

      expect(page.status()).toBe('Impossible de charger la dépense. Veuillez réessayer.');
      expect(text(page.form()?.querySelector('h2'))).toBe('Nouvelle dépense');
    });

    it('reloads the expenses and the header once an expense is changed', async () => {
      const api = fakeApi();
      const expensesApi = fakeExpensesApi();
      const page = await render(api, expensesApi);
      await page.click(page.button('Modifier', page.rows()[0]));

      await page.click(page.button('Enregistrer', page.form() ?? undefined));

      expect(expensesApi.update).toHaveBeenCalled();
      expect(page.status()).toBe('Dépense modifiée.');
      expect(text(page.form()?.querySelector('h2'))).toBe('Nouvelle dépense');
      expect(expensesApi.list).toHaveBeenCalledTimes(2);
      expect(api.get).toHaveBeenCalledTimes(2);
    });

    it('deletes an expense once confirmed, then reloads the expenses and the header', async () => {
      const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const api = fakeApi();
      const expensesApi = fakeExpensesApi();
      const page = await render(api, expensesApi);

      await page.click(page.button('Supprimer', page.rows()[0]));

      expect(confirm).toHaveBeenCalledWith('Supprimer la dépense « Taxi » ?');
      expect(expensesApi.delete).toHaveBeenCalledWith('1', 'e1');
      expect(expensesApi.list).toHaveBeenCalledTimes(2);
      expect(api.get).toHaveBeenCalledTimes(2);
    });

    it('goes back to adding an expense once the one being edited is deleted', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const page = await render();
      await page.click(page.button('Modifier', page.rows()[0]));

      await page.click(page.button('Supprimer', page.rows()[0]));

      expect(text(page.form()?.querySelector('h2'))).toBe('Nouvelle dépense');
    });

    it('keeps the expense when the deletion is cancelled', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const expensesApi = fakeExpensesApi();
      const page = await render(fakeApi(), expensesApi);

      await page.click(page.button('Supprimer', page.rows()[0]));

      expect(expensesApi.delete).not.toHaveBeenCalled();
    });

    it("turns read-only when an addition finds the report's user deleted", async () => {
      const api = fakeApi();
      const expensesApi = fakeExpensesApi();
      expensesApi.create.mockImplementation(() =>
        throwError(() => new HttpErrorResponse({ status: 422, error: { code: 'expense_report.deleted_user' } }))
      );
      const page = await render(api, expensesApi);
      api.get.mockImplementation(() => of({ ...october, isReadOnly: true }));
      const form = page.form() as HTMLElement;
      for (const [id, value] of [
        ['date', '2025-10-15'],
        ['description', 'Taxi'],
        ['amount', '10'],
        ['street', '12 rue de Rivoli'],
        ['postalCode', '75001'],
        ['city', 'Paris'],
      ]) {
        const input = form.querySelector(`#${id}`) as HTMLInputElement;
        input.value = value;
        input.dispatchEvent(new Event('input'));
      }

      form.querySelector('form')?.dispatchEvent(new Event('submit'));
      page.harness.detectChanges();

      expect(api.get).toHaveBeenCalledTimes(2);
      expect(page.form()).toBeNull();
      expect(page.element().textContent).toContain(
        "L'utilisateur de cette note de frais a été supprimé : elle est en lecture seule."
      );
    });

    it('drops the notice of the last save once the next one is refused', async () => {
      const expensesApi = fakeExpensesApi();
      const page = await render(fakeApi(), expensesApi);
      const form = page.form() as HTMLElement;
      const submitValidExpense = async () => {
        for (const [id, value] of [
          ['date', '2025-10-15'],
          ['description', 'Taxi'],
          ['amount', '10'],
          ['street', '12 rue de Rivoli'],
          ['postalCode', '75001'],
          ['city', 'Paris'],
        ]) {
          const input = form.querySelector(`#${id}`) as HTMLInputElement;
          input.value = value;
          input.dispatchEvent(new Event('input'));
        }
        form.querySelector('form')?.dispatchEvent(new Event('submit'));
        page.harness.detectChanges();
        await page.harness.fixture.whenStable();
        page.harness.detectChanges();
      };
      await submitValidExpense();
      expect(page.status()).toBe('Dépense ajoutée.');
      expensesApi.create.mockImplementation(() =>
        throwError(() => new HttpErrorResponse({ status: 422, error: { code: 'expense.quota_exceeded' } }))
      );

      await submitValidExpense();

      expect(page.status()).toBeUndefined();
      expect(page.alert()).toBe(
        "Le quota mensuel de dépenses de cet utilisateur est atteint : impossible d'ajouter cette dépense."
      );
    });

    it('shows a read-only report without the form nor the expense actions', async () => {
      const api = fakeApi();
      api.get.mockImplementation(() => of({ ...october, isReadOnly: true }));

      const page = await render(api);

      expect(page.element().textContent).toContain(
        "L'utilisateur de cette note de frais a été supprimé : elle est en lecture seule."
      );
      expect(page.form()).toBeNull();
      expect(page.button('Modifier')).toBeUndefined();
      expect(text(page.element())).toContain('3 dépenses');
      expect(text(page.element())).not.toContain('ce mois-ci');
      expect(page.cells(page.rows()[0])).toHaveLength(4);
    });
  });
});
