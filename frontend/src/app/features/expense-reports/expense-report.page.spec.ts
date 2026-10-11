import { registerLocaleData } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import localeFr from '@angular/common/locales/fr';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
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
};

function httpError(status: number): Observable<never> {
  return throwError(() => new HttpErrorResponse({ status }));
}

function fakeApi() {
  return {
    get: vi.fn((): Observable<ExpenseReportDetails> => of(october)),
    delete: vi.fn((): Observable<void> => of(undefined)),
  };
}

async function render(api = fakeApi()) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'expense-reports', children: [] },
        { path: 'expense-reports/:id', component: ExpenseReportPage },
      ]),
      { provide: ExpenseReportsApi, useValue: api },
      { provide: LOCALE_ID, useValue: 'fr-FR' },
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/expense-reports/1', ExpenseReportPage);
  harness.detectChanges();
  const element = () => harness.routeNativeElement as HTMLElement;
  const deleteButton = () =>
    Array.from(element().querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Supprimer'
    ) as HTMLButtonElement;

  return {
    element,
    deleteButton,
    heading: () => element().querySelector('h1')?.textContent?.trim(),
    alert: () => element().querySelector('[role="alert"]')?.textContent?.trim(),
    remove: async () => {
      deleteButton().click();
      harness.detectChanges();
      await harness.fixture.whenStable();
    },
  };
}

describe('ExpenseReportPage', () => {
  afterEach(() => vi.restoreAllMocks());

  it('shows the title and the month of the report', async () => {
    const api = fakeApi();

    const page = await render(api);

    expect(api.get).toHaveBeenCalledWith('1');
    expect(page.heading()).toBe('Juste Leblanc - Octobre 2025');
    expect(page.element().textContent).toContain('Mois : octobre 2025');
  });

  it('links back to the list', async () => {
    const page = await render();

    const back = page.element().querySelector('a');
    expect(back?.textContent?.trim()).toBe('← Notes de frais');
    expect(back?.getAttribute('href')).toBe('/expense-reports');
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
