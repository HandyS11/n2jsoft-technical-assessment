import { CurrencyPipe, formatDate } from '@angular/common';
import { Component, computed, inject, LOCALE_ID, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, EMPTY, finalize, Observable, of, Subject, switchMap } from 'rxjs';
import { pagedList } from '../../core/paged-list';
import { PaginationComponent } from '../../core/pagination.component';
import { hasStatus } from '../../core/problem-details';
import { ExpenseDetails, ExpenseSummary, formatBillingAddress } from '../expenses/expense';
import { ExpenseFormComponent } from '../expenses/expense-form.component';
import { ExpensesApi } from '../expenses/expenses.api';
import { confirmDeletion } from './confirm-deletion';
import { ExpenseReportDetails } from './expense-report-details';
import { ExpenseReportsApi } from './expense-reports.api';

/** "Par lot de 5": the README's batch size. */
const expensesPerPage = 5;

/** "1 dépense", "3 dépenses"; French keeps 0 singular. */
function expenseCount(count: number): string {
  return `${count} ${count > 1 ? 'dépenses' : 'dépense'}`;
}

@Component({
  selector: 'app-expense-report-page',
  imports: [CurrencyPipe, ExpenseFormComponent, PaginationComponent, RouterLink],
  template: `
    <a routerLink="/expense-reports" class="link text-sm">← Notes de frais</a>
    @if (loadError(); as message) {
      <p role="alert" class="mt-4">{{ message }}</p>
    }
    @if (deleteError(); as message) {
      <p role="alert" class="mt-4">{{ message }}</p>
    }
    @if (report(); as report) {
      <div class="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 class="text-2xl font-semibold text-slate-900">{{ report.title }}</h1>
          @if (!report.isReadOnly) {
            <a [routerLink]="['/users', report.userId, 'edit']" class="link text-sm">Voir l'utilisateur</a>
          }
        </div>
        <button type="button" class="btn-danger" [disabled]="deleting()" (click)="remove(report)">
          Supprimer la note de frais
        </button>
      </div>
      <div class="mt-4 flex flex-wrap gap-2 text-sm">
        <p class="rounded-md border border-slate-200 bg-white px-3 py-1.5">Mois : {{ monthLabel() }}</p>
        <p class="rounded-md border border-slate-200 bg-white px-3 py-1.5">{{ countLabel() }}</p>
        <p class="rounded-md border border-slate-200 bg-white px-3 py-1.5 font-medium">
          Total : {{ report.total | currency: 'EUR' }}
        </p>
      </div>

      <section class="my-8">
        @if (report.isReadOnly) {
          <p class="rounded-md border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            L'utilisateur de cette note de frais a été supprimé : elle est en lecture seule.
          </p>
        } @else {
          <app-expense-form
            class="card block p-6"
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
          <p role="status" class="mt-4">{{ message }}</p>
        }
      </section>

      <section>
        <h2 class="mb-3 text-lg font-semibold text-slate-900">Dépenses</h2>
        @if (expenses.failed()) {
          <p role="alert" class="mb-4">Impossible de charger les dépenses.</p>
        }
        @if (expenses.actionFailed()) {
          <p role="alert" class="mb-4">L'opération a échoué. Veuillez réessayer.</p>
        }
        @if (expenses.page(); as page) {
          @if (page.items.length > 0) {
            <div class="card overflow-x-auto">
              <table>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Description</th>
                    <th class="text-right">Montant</th>
                    <th>Adresse de facturation</th>
                    @if (!report.isReadOnly) {
                      <th class="text-right">Actions</th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (expense of page.items; track expense.id) {
                    <tr [class.bg-indigo-50]="editing()?.id === expense.id">
                      <td class="whitespace-nowrap">{{ dateLabel(expense) }}</td>
                      <td class="font-medium text-slate-900">{{ expense.description }}</td>
                      <td class="text-right whitespace-nowrap tabular-nums">{{ expense.amount | currency: 'EUR' }}</td>
                      <td class="text-slate-600">{{ addressLabel(expense) }}</td>
                      @if (!report.isReadOnly) {
                        <td>
                          <div class="flex justify-end gap-2">
                            <button type="button" class="btn-sm" [disabled]="expenses.busy()" (click)="edit(expense)">
                              Modifier
                            </button>
                            <button
                              type="button"
                              class="btn-sm btn-danger"
                              [disabled]="expenses.busy()"
                              (click)="removeExpense(expense)"
                            >
                              Supprimer
                            </button>
                          </div>
                        </td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <app-pagination [list]="expenses" />
          } @else {
            <p class="card muted p-8 text-center">Aucune dépense.</p>
          }
        } @else if (!expenses.failed()) {
          <p class="muted">Chargement…</p>
        }
      </section>
    } @else if (!loadError()) {
      <p class="muted mt-4">Chargement…</p>
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
        if (hasStatus(error, 404)) {
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
    this.loadError.set(hasStatus(error, 404) ? 'Note de frais introuvable.' : 'Impossible de charger la note de frais.');
    return EMPTY;
  }

  private expenseLoadFailed(error: unknown): Observable<never> {
    if (hasStatus(error, 404)) {
      // Deleted meanwhile: the list shows what is left.
      this.notice.set('Dépense introuvable.');
      this.reloadExpenses();
    } else {
      this.notice.set('Impossible de charger la dépense. Veuillez réessayer.');
    }
    return EMPTY;
  }
}
