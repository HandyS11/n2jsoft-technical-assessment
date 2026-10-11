import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PagedResult } from '../../core/paged-result';
import { ExpenseDetails, ExpenseSummary, SaveExpense } from './expense';
import { ExpensesApi } from './expenses.api';

const reportId = 'r1';
const taxi: SaveExpense = {
  date: '2025-10-15',
  description: 'Taxi',
  amount: 23.5,
  billingAddress: { storeName: null, street: '12 rue de Rivoli', postalCode: '75001', city: 'Paris' },
};
const details: ExpenseDetails = { ...taxi, id: 'e1', version: 7 };

describe('ExpensesApi', () => {
  let api: ExpensesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ExpensesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("lists a page of the report's expenses", () => {
    const page: PagedResult<ExpenseSummary> = { items: [{ ...taxi, id: 'e1' }], page: 2, pageSize: 5, totalCount: 6 };
    let received: PagedResult<ExpenseSummary> | undefined;

    api.list(reportId, 2, 5).subscribe((result) => (received = result));

    const request = http.expectOne('/api/v1/expense-reports/r1/expenses?page=2&pageSize=5');
    expect(request.request.method).toBe('GET');
    request.flush(page);
    expect(received).toEqual(page);
  });

  it('gets an expense', () => {
    let received: ExpenseDetails | undefined;

    api.get(reportId, 'e1').subscribe((result) => (received = result));

    const request = http.expectOne('/api/v1/expense-reports/r1/expenses/e1');
    expect(request.request.method).toBe('GET');
    request.flush(details);
    expect(received).toEqual(details);
  });

  it('creates an expense with POST', () => {
    let received: ExpenseDetails | undefined;

    api.create(reportId, taxi).subscribe((result) => (received = result));

    const request = http.expectOne('/api/v1/expense-reports/r1/expenses');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(taxi);
    request.flush(details);
    expect(received).toEqual(details);
  });

  it('updates an expense with PUT and the version read as If-Match', () => {
    api.update(reportId, 'e1', taxi, 7).subscribe();

    const request = http.expectOne('/api/v1/expense-reports/r1/expenses/e1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(taxi);
    expect(request.request.headers.get('If-Match')).toBe('"7"');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('deletes an expense with DELETE', () => {
    api.delete(reportId, 'e1').subscribe();

    const request = http.expectOne('/api/v1/expense-reports/r1/expenses/e1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });
});
