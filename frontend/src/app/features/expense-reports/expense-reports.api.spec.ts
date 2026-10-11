import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PagedResult } from '../../core/paged-result';
import {
  CreateExpenseReport,
  ExpenseReportDetails,
} from './expense-report-details';
import { ExpenseReportSummary } from './expense-report-summary';
import { ExpenseReportsApi } from './expense-reports.api';

const id = '0199d5a8-7c1e-7b3a-9f2e-3c4d5e6f7a8b';
const userId = '7d9f5f3c-2f1e-4a43-9a43-4d1f0c6a1b2e';
const october: CreateExpenseReport = { userId, year: 2025, month: 10 };
const details: ExpenseReportDetails = {
  ...october,
  id,
  title: 'Juste Leblanc - Octobre 2025',
  expenseCount: 0,
  total: 0,
  monthlyExpenseQuota: 10,
  isReadOnly: false,
};

describe('ExpenseReportsApi', () => {
  let api: ExpenseReportsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ExpenseReportsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists a page of reports with GET /api/v1/expense-reports', () => {
    const reports: PagedResult<ExpenseReportSummary> = {
      items: [
        { id, title: 'Juste Leblanc - Octobre 2025', year: 2025, month: 10 },
      ],
      page: 2,
      pageSize: 20,
      totalCount: 21,
    };
    let result: PagedResult<ExpenseReportSummary> | undefined;

    api.list(2, 20).subscribe((value) => (result = value));

    const request = http.expectOne(
      '/api/v1/expense-reports?page=2&pageSize=20',
    );
    expect(request.request.method).toBe('GET');
    request.flush(reports);
    expect(result).toEqual(reports);
  });

  it('gets a report with GET /api/v1/expense-reports/:id', () => {
    let result: ExpenseReportDetails | undefined;

    api.get(id).subscribe((value) => (result = value));

    const request = http.expectOne(`/api/v1/expense-reports/${id}`);
    expect(request.request.method).toBe('GET');
    request.flush(details);
    expect(result).toEqual(details);
  });

  it('creates a report with POST /api/v1/expense-reports', () => {
    let result: ExpenseReportDetails | undefined;

    api.create(october).subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/expense-reports');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(october);
    request.flush(details, { status: 201, statusText: 'Created' });
    expect(result).toEqual(details);
  });

  it('deletes a report with DELETE /api/v1/expense-reports/:id', () => {
    api.delete(id).subscribe();

    const request = http.expectOne(`/api/v1/expense-reports/${id}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });
});
