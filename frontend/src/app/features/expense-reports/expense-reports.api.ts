import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import { CreateExpenseReport, ExpenseReportDetails } from './expense-report-details';
import { ExpenseReportSummary } from './expense-report-summary';

const expenseReportsUrl = '/api/v1/expense-reports';

@Injectable({ providedIn: 'root' })
export class ExpenseReportsApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Observable<PagedResult<ExpenseReportSummary>> {
    return this.http.get<PagedResult<ExpenseReportSummary>>(expenseReportsUrl, {
      params: { page, pageSize },
    });
  }

  get(id: string): Observable<ExpenseReportDetails> {
    return this.http.get<ExpenseReportDetails>(`${expenseReportsUrl}/${id}`);
  }

  /** Refused with a 422 when the user is inactive or already has a report for that month. */
  create(report: CreateExpenseReport): Observable<ExpenseReportDetails> {
    return this.http.post<ExpenseReportDetails>(expenseReportsUrl, report);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${expenseReportsUrl}/${id}`);
  }
}
