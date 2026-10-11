import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/paged-result';
import { ExpenseDetails, ExpenseSummary, SaveExpense } from './expense';

function expensesUrl(reportId: string): string {
  return `/api/v1/expense-reports/${reportId}/expenses`;
}

@Injectable({ providedIn: 'root' })
export class ExpensesApi {
  private readonly http = inject(HttpClient);

  list(reportId: string, page: number, pageSize: number): Observable<PagedResult<ExpenseSummary>> {
    return this.http.get<PagedResult<ExpenseSummary>>(expensesUrl(reportId), { params: { page, pageSize } });
  }

  get(reportId: string, id: string): Observable<ExpenseDetails> {
    return this.http.get<ExpenseDetails>(`${expensesUrl(reportId)}/${id}`);
  }

  /** Refused with a 422 when the quota is reached, the date is outside the report's month or the user is deleted. */
  create(reportId: string, expense: SaveExpense): Observable<ExpenseDetails> {
    return this.http.post<ExpenseDetails>(expensesUrl(reportId), expense);
  }

  /** Refused with 412 when the expense changed since `version` was read. */
  update(reportId: string, id: string, expense: SaveExpense, version: number): Observable<void> {
    return this.http.put<void>(`${expensesUrl(reportId)}/${id}`, expense, {
      headers: { 'If-Match': `"${version}"` },
    });
  }

  delete(reportId: string, id: string): Observable<void> {
    return this.http.delete<void>(`${expensesUrl(reportId)}/${id}`);
  }
}
