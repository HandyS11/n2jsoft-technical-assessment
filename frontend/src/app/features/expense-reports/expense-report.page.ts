import { CurrencyPipe, formatDate } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, LOCALE_ID, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, EMPTY, finalize, Observable, of, Subject, switchMap } from 'rxjs';
import { pagedList } from '../../core/paged-list';
import { PaginationComponent } from '../../core/pagination.component';
import { ExpenseDetails, ExpenseSummary, formatBillingAddress } from '../expenses/expense';
import { ExpenseFormComponent } from '../expenses/expense-form.component';
import { ExpensesApi } from '../expenses/expenses.api';
import { confirmDeletion } from './confirm-deletion';
import { ExpenseReportDetails } from './expense-report-details';
import { ExpenseReportsApi } from './expense-reports.api';

/** "Par lot de 5": the README's batch size. */
const expensesPerPage = 5;

function isNotFound(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 404;
}

/** "1 dépense", "3 dépenses"; French keeps 0 singular. */
function expenseCount(count: number): string {
  return `${count} ${count > 1 ? 'dépenses' : 'dépense'}`;
}

@Component({
  selector: 'app-expense-report-page',
  imports: [CurrencyPipe, ExpenseFormComponent, PaginationComponent, RouterLink],
  template: `
    <a routerLink="/expense-reports" class="underline">← Notes de frais</a>
    @if (loadError(); as message) {
      <p role="alert">{{ message }}</p>
    }
    @if (deleteError(); as message) {
      <p role="alert">{{ message }}</p>
    }
    @if (report(); as report) {
      <h1 class="my-4 text-2xl font-semibold">{{ report.title }}</h1>
      <p>Mois : {{ monthLabel() }}</p>
      <p>{{ countLabel() }}</p>
      <p>Total : {{ report.total | currency: 'EUR' }}</p>
      <button type="button" class="mt-4" [disabled]="deleting()" (click)="remove(report)">
        Supprimer la note de frais
      </button>

      <section class="my-8">
        @if (report.isReadOnly) {
          <p>L'utilisateur de cette note de frais a été supprimé : elle est en lecture seule.</p>
        } @else {
          <app-expense-form
            [reportId]="report.id"
            [year]="report.year"
            [month]="report.month"
            [expense]="editing()"
            (submitted)="notice.set(null)"
            (saved)="onSaved($event)"
            (cancelled)="stopEditing()"
            (reportChanged)="loadReport()"
          />
        }
        @if (notice(); as message) {
          <p role="status" class="mt-2">{{ message }}</p>
        }
      </section>

      <section>
        <h2 class="mb-2 text-xl font-semibold">Dépenses</h2>
        @if (expenses.failed()) {
          <p role="alert">Impossible de charger les dépenses.</p>
        }
        @if (expenses.actionFailed()) {
          <p role="alert">L'opération a échoué. Veuillez réessayer.</p>
        }
        @if (expenses.page(); as page) {
          @if (page.items.length > 0) {
            <table class="w-full text-left">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Description</th>
                  <th>Montant</th>
                  <th>Adresse de facturation</th>
                  @if (!report.isReadOnly) {
                    <th>Actions</th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (expense of page.items; track expense.id) {
                  <tr>
                    <td>{{ dateLabel(expense) }}</td>
                    <td>{{ expense.description }}</td>
                    <td>{{ expense.amount | currency: 'EUR' }}</td>
                    <td>{{ addressLabel(expense) }}</td>
                    @if (!report.isReadOnly) {
                      <td class="flex gap-2">
                        <button type="button" [disabled]="expenses.busy()" (click)="edit(expense)">Modifier</button>
                        <button type="button" [disabled]="expenses.busy()" (click)="removeExpense(expense)">
                          Supprimer
                        </button>
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
            <app-pagination [list]="expenses" />
          } @else {
            <p>Aucune dépense.</p>
          }
        } @else if (!expenses.failed()) {
          <p>Chargement…</p>
        }
      </section>
    } @else if (!loadError()) {
      <p>Chargement…</p>
    }
  `,
})
export default class ExpenseReportPage {
  private readonly api = inject(ExpenseReportsApi);
  private readonly expensesApi = inject(ExpensesApi);
  private readonly router = inject(Router);
  private readonly locale = inject(LOCALE_ID);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly report = signal<ExpenseReportDetails | undefined>(undefined);
  /** Why the report could not be read, until it is read again. */
  protected readonly loadError = signal<string | null>(null);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly deleting = signal(false);
  /** The expense the form edits, as loaded; undefined while the form adds one. */
  protected readonly editing = signal<ExpenseDetails | undefined>(undefined);
  /** What the last change did ("Dépense ajoutée."), until the next one. */
  protected readonly notice = signal<string | null>(null);
  protected readonly expenses = pagedList((page) => this.expensesApi.list(this.id, page, expensesPerPage));
  private readonly reportReads = new Subject<void>();
  /** The id of the expense to load into the form, or undefined to go back to adding one. */
  private readonly editRequests = new Subject<string | undefined>();

  /** "Octobre 2025", capitalised like the month in the title (the locale writes "octobre 2025"). */
  protected readonly monthLabel = computed(() => {
    const report = this.report();
    if (!report) {
      return '';
    }
    // The first day of the report's month, in local time.
    const label = formatDate(new Date(report.year, report.month - 1, 1), 'MMMM y', this.locale);
    return label.charAt(0).toUpperCase() + label.slice(1);
  });

  /** "3 dépenses sur 10 ce mois-ci"; a read-only report has no quota left to show. */
  protected readonly countLabel = computed(() => {
    const report = this.report();
    if (!report) {
      return '';
    }
    const count = expenseCount(report.expenseCount);
    return report.isReadOnly ? count : `${count} sur ${report.monthlyExpenseQuota} ce mois-ci`;
  });

  constructor() {
    this.reportReads
      .pipe(
        // Only the latest read counts: an answer arriving late must not replace the report read since.
        switchMap(() => this.api.get(this.id).pipe(catchError((error: unknown) => this.reportLoadFailed(error)))),
        takeUntilDestroyed()
      )
      .subscribe((report) => {
        this.report.set(report);
        this.loadError.set(null);
      });
    this.editRequests
      .pipe(
        // Only the last expense asked for is edited: an answer arriving late must not load another one in the form.
        switchMap((expenseId) =>
          expenseId === undefined
            ? of(undefined)
            : this.expensesApi
                .get(this.id, expenseId)
                .pipe(catchError((error: unknown) => this.expenseLoadFailed(error)))
        ),
        takeUntilDestroyed()
      )
      .subscribe((expense) => this.editing.set(expense));
    this.loadReport();
  }

  /** Reads the report again: its counts and total after a change, or its read-only state. */
  protected loadReport(): void {
    this.reportReads.next();
  }

  /** "mercredi 15 octobre 2025". A "2025-10-15" date is read as a local day, never shifted by a time zone. */
  protected dateLabel(expense: ExpenseSummary): string {
    return formatDate(expense.date, 'EEEE d MMMM y', this.locale);
  }

  protected addressLabel(expense: ExpenseSummary): string {
    return formatBillingAddress(expense.billingAddress);
  }

  protected onSaved(change: 'created' | 'updated'): void {
    this.stopEditing();
    this.notice.set(change === 'created' ? 'Dépense ajoutée.' : 'Dépense modifiée.');
    this.reloadExpenses();
  }

  protected edit(expense: ExpenseSummary): void {
    this.notice.set(null);
    this.editRequests.next(expense.id);
  }

  /** Back to adding an expense; an expense still loading is not put in the form. */
  protected stopEditing(): void {
    this.editRequests.next(undefined);
  }

  protected removeExpense(expense: ExpenseSummary): void {
    if (!window.confirm(`Supprimer la dépense « ${expense.description} » ?`)) {
      return;
    }
    this.notice.set(null);
    if (this.editing()?.id === expense.id) {
      this.stopEditing();
    }
    // Whether it worked or not, the counts and the read-only state may have changed.
    this.expenses.run(this.expensesApi.delete(this.id, expense.id).pipe(finalize(() => this.loadReport())));
  }

  protected remove(report: ExpenseReportDetails): void {
    if (!confirmDeletion(report.title)) {
      return;
    }
    this.deleting.set(true);
    this.deleteError.set(null);
    this.api.delete(report.id).subscribe({
      next: () => {
        void this.router.navigateByUrl('/expense-reports');
      },
      error: (error: unknown) => {
        // Deleted meanwhile (another tab): it is gone, as asked.
        if (isNotFound(error)) {
          void this.router.navigateByUrl('/expense-reports');
          return;
        }
        this.deleting.set(false);
        this.deleteError.set('La suppression a échoué. Veuillez réessayer.');
      },
    });
  }

  private reloadExpenses(): void {
    this.expenses.load(this.expenses.page()?.page ?? 1);
    this.loadReport();
  }

  private reportLoadFailed(error: unknown): Observable<never> {
    this.loadError.set(isNotFound(error) ? 'Note de frais introuvable.' : 'Impossible de charger la note de frais.');
    return EMPTY;
  }

  private expenseLoadFailed(error: unknown): Observable<never> {
    if (isNotFound(error)) {
      // Deleted meanwhile: the list shows what is left.
      this.notice.set('Dépense introuvable.');
      this.reloadExpenses();
    } else {
      this.notice.set('Impossible de charger la dépense. Veuillez réessayer.');
    }
    return EMPTY;
  }
}
