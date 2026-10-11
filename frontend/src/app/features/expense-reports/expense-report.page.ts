import { CurrencyPipe, formatDate } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, LOCALE_ID, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { pagedList } from '../../core/paged-list';
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
  imports: [CurrencyPipe, ExpenseFormComponent, RouterLink],
  template: `
    <a routerLink="/expense-reports" class="underline">← Notes de frais</a>
    @if (error(); as message) {
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
            (saved)="onSaved($event)"
            (cancelled)="editing.set(undefined)"
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
            <nav aria-label="Pagination" class="mt-4 flex items-center gap-4">
              <button
                type="button"
                [disabled]="expenses.busy() || page.page <= 1"
                (click)="expenses.load(page.page - 1)"
              >
                Précédent
              </button>
              <span>Page {{ page.page }} sur {{ expenses.pageCount() }}</span>
              <button
                type="button"
                [disabled]="expenses.busy() || page.page >= expenses.pageCount()"
                (click)="expenses.load(page.page + 1)"
              >
                Suivant
              </button>
            </nav>
          } @else {
            <p>Aucune dépense.</p>
          }
        } @else if (!expenses.failed()) {
          <p>Chargement…</p>
        }
      </section>
    } @else if (!error()) {
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
  protected readonly error = signal<string | null>(null);
  protected readonly deleting = signal(false);
  /** The expense the form edits, as loaded; undefined while the form adds one. */
  protected readonly editing = signal<ExpenseDetails | undefined>(undefined);
  /** What the last change did ("Dépense ajoutée."), until the next one. */
  protected readonly notice = signal<string | null>(null);
  protected readonly expenses = pagedList((page) => this.expensesApi.list(this.id, page, expensesPerPage));

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
    this.loadReport();
  }

  /** Reads the report again: its counts and total after a change, or its read-only state. */
  protected loadReport(): void {
    this.api.get(this.id).subscribe({
      next: (report) => this.report.set(report),
      error: (error: unknown) =>
        this.error.set(
          isNotFound(error) ? 'Note de frais introuvable.' : 'Impossible de charger la note de frais.'
        ),
    });
  }

  /** "mercredi 15 octobre 2025". A "2025-10-15" date is read as a local day, never shifted by a time zone. */
  protected dateLabel(expense: ExpenseSummary): string {
    return formatDate(expense.date, 'EEEE d MMMM y', this.locale);
  }

  protected addressLabel(expense: ExpenseSummary): string {
    return formatBillingAddress(expense.billingAddress);
  }

  protected onSaved(change: 'created' | 'updated'): void {
    this.editing.set(undefined);
    this.notice.set(change === 'created' ? 'Dépense ajoutée.' : 'Dépense modifiée.');
    this.reloadExpenses();
  }

  protected edit(expense: ExpenseSummary): void {
    this.notice.set(null);
    this.expensesApi.get(this.id, expense.id).subscribe({
      next: (details) => this.editing.set(details),
      error: () => {
        // Most likely deleted meanwhile: the list shows what is left.
        this.notice.set('Dépense introuvable.');
        this.reloadExpenses();
      },
    });
  }

  protected removeExpense(expense: ExpenseSummary): void {
    if (!window.confirm(`Supprimer la dépense « ${expense.description} » ?`)) {
      return;
    }
    this.notice.set(null);
    if (this.editing()?.id === expense.id) {
      this.editing.set(undefined);
    }
    // Whether it worked or not, the counts and the read-only state may have changed.
    this.expenses.run(this.expensesApi.delete(this.id, expense.id).pipe(finalize(() => this.loadReport())));
  }

  protected remove(report: ExpenseReportDetails): void {
    if (!confirmDeletion(report.title)) {
      return;
    }
    this.deleting.set(true);
    this.error.set(null);
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
        this.error.set('La suppression a échoué. Veuillez réessayer.');
      },
    });
  }

  private reloadExpenses(): void {
    this.expenses.load(this.expenses.page()?.page ?? 1);
    this.loadReport();
  }
}
