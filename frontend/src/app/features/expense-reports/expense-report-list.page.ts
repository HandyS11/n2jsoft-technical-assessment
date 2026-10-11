import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, EMPTY, Subject, switchMap } from 'rxjs';
import { PagedResult, pageCountOf } from '../../core/paged-result';
import { businessRuleCode } from '../../core/problem-details';
import { UserSummary } from '../users/user-summary';
import { UsersApi } from '../users/users.api';
import { confirmDeletion } from './confirm-deletion';
import { ExpenseReportSummary } from './expense-report-summary';
import { ExpenseReportsApi } from './expense-reports.api';

const pageSize = 20;

/**
 * An <input type="month"> value ("2025-10") within the API's bounds (ReportMonth.MinYear and MaxYear). Checked here
 * because some browsers show the field as plain text, where anything can be typed.
 */
const monthPattern = /^(20[0-9]{2}|2100)-(0[1-9]|1[0-2])$/;

const ruleMessages = new Map<string, string>([
  [
    'expense_report.inactive_user',
    'Cet utilisateur est inactif : il ne peut pas être affecté à une note de frais.',
  ],
  ['expense_report.already_exists', 'Une note de frais existe déjà pour cet utilisateur et ce mois.'],
]);

/** The current month, in the browser's time zone, as an <input type="month"> value. */
function currentMonth(): string {
  const today = new Date();
  return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`;
}

function createErrorMessage(error: unknown): string {
  const ruleMessage = ruleMessages.get(businessRuleCode(error) ?? '');
  if (ruleMessage !== undefined) {
    return ruleMessage;
  }
  if (error instanceof HttpErrorResponse && error.status === 404) {
    // The user was deleted since the active users were loaded.
    return 'Utilisateur introuvable.';
  }
  return "L'opération a échoué. Veuillez réessayer.";
}

@Component({
  selector: 'app-expense-report-list-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <h1 class="mb-4 text-2xl font-semibold">Notes de frais</h1>

    <section class="mb-8">
      <h2 class="mb-2 text-xl font-semibold">Nouvelle note de frais</h2>
      @if (usersFailed()) {
        <p role="alert">Impossible de charger les utilisateurs.</p>
      } @else if (activeUsers(); as users) {
        @if (users.length > 0) {
          <form [formGroup]="form" (ngSubmit)="create()" novalidate class="flex max-w-md flex-col gap-2">
            <label for="userId">Utilisateur</label>
            <select id="userId" formControlName="userId">
              <option value="" disabled>Choisissez un utilisateur</option>
              @for (user of users; track user.id) {
                <option [value]="user.id">{{ user.name }}</option>
              }
            </select>
            @if (errorFor('userId'); as message) {
              <p class="field-error text-sm text-red-700">{{ message }}</p>
            }

            <label for="month">Mois</label>
            <input id="month" type="month" min="2000-01" max="2100-12" formControlName="month" />
            @if (errorFor('month'); as message) {
              <p class="field-error text-sm text-red-700">{{ message }}</p>
            }

            @if (createError(); as message) {
              <p role="alert">{{ message }}</p>
            }
            <div class="mt-2">
              <button type="submit" [disabled]="creating()">Créer</button>
            </div>
          </form>
        } @else {
          <p>Aucun utilisateur actif.</p>
        }
      } @else {
        <p>Chargement…</p>
      }
    </section>

    @if (failed()) {
      <p role="alert">Impossible de charger les notes de frais.</p>
    }
    @if (actionFailed()) {
      <p role="alert">L'opération a échoué. Veuillez réessayer.</p>
    }
    @if (page(); as page) {
      @if (page.items.length > 0) {
        <table class="w-full text-left">
          <thead>
            <tr>
              <th>Intitulé</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (report of page.items; track report.id) {
              <tr>
                <td>
                  <a [routerLink]="['/expense-reports', report.id]" class="underline">{{ report.title }}</a>
                </td>
                <td>
                  <button type="button" [disabled]="busy()" (click)="remove(report)">Supprimer</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
        <nav aria-label="Pagination" class="mt-4 flex items-center gap-4">
          <button type="button" [disabled]="busy() || page.page <= 1" (click)="load(page.page - 1)">
            Précédent
          </button>
          <span>Page {{ page.page }} sur {{ pageCount() }}</span>
          <button
            type="button"
            [disabled]="busy() || page.page >= pageCount()"
            (click)="load(page.page + 1)"
          >
            Suivant
          </button>
        </nav>
      } @else {
        <p>Aucune note de frais.</p>
      }
    } @else if (!failed()) {
      <p>Chargement…</p>
    }
  `,
})
export default class ExpenseReportListPage {
  private readonly reportsApi = inject(ExpenseReportsApi);
  private readonly router = inject(Router);

  protected readonly page = signal<PagedResult<ExpenseReportSummary> | undefined>(undefined);
  protected readonly failed = signal(false);
  protected readonly actionFailed = signal(false);
  /** A deletion runs, until the page reloaded after it shows: a second click would act on a stale row. */
  protected readonly busy = signal(false);
  protected readonly pageCount = computed(() => {
    const page = this.page();
    return page ? pageCountOf(page) : 1;
  });
  private readonly pageRequests = new Subject<number>();

  /** Undefined while they load. */
  protected readonly activeUsers = signal<UserSummary[] | undefined>(undefined);
  protected readonly usersFailed = signal(false);
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    userId: ['', Validators.required],
    month: [currentMonth(), [Validators.required, Validators.pattern(monthPattern)]],
  });

  constructor() {
    inject(UsersApi)
      .listAllActive()
      .subscribe({
        next: (users) => this.activeUsers.set(users),
        error: () => this.usersFailed.set(true),
      });
    this.pageRequests
      .pipe(
        // Only the latest request counts: an answer arriving late must not replace the page asked for since.
        switchMap((pageNumber) =>
          this.reportsApi.list(pageNumber, pageSize).pipe(
            catchError(() => {
              this.failed.set(true);
              this.busy.set(false);
              return EMPTY;
            })
          )
        ),
        takeUntilDestroyed()
      )
      .subscribe((page) => this.show(page));
    this.load(1);
  }

  protected load(pageNumber: number): void {
    this.pageRequests.next(pageNumber);
  }

  protected errorFor(field: 'userId' | 'month'): string | null {
    const control = this.form.controls[field];
    if (control.valid || !control.touched) {
      return null;
    }
    return field === 'userId'
      ? 'Choisissez un utilisateur.'
      : 'Indiquez un mois au format AAAA-MM, entre 2000-01 et 2100-12.';
  }

  protected create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { userId, month } = this.form.getRawValue();
    this.creating.set(true);
    this.createError.set(null);
    this.reportsApi
      .create({ userId, year: Number(month.slice(0, 4)), month: Number(month.slice(5, 7)) })
      .subscribe({
        next: (report) => {
          void this.router.navigate(['/expense-reports', report.id]);
        },
        error: (error: unknown) => {
          this.creating.set(false);
          this.createError.set(createErrorMessage(error));
        },
      });
  }

  protected remove(report: ExpenseReportSummary): void {
    if (!confirmDeletion(report.title)) {
      return;
    }
    this.busy.set(true);
    this.actionFailed.set(false);
    this.reportsApi.delete(report.id).subscribe({
      next: () => this.load(this.page()?.page ?? 1),
      error: () => {
        // The report may be gone (deleted in another tab): reload so a stale row does not stay.
        this.actionFailed.set(true);
        this.load(this.page()?.page ?? 1);
      },
    });
  }

  private show(page: PagedResult<ExpenseReportSummary>): void {
    // Reports were deleted past the page shown: show the last page left rather than an empty one. Always an earlier
    // page, so this ends even if more reports go meanwhile.
    const lastPage = pageCountOf(page);
    if (page.items.length === 0 && page.page > lastPage) {
      this.load(lastPage);
      return;
    }
    this.page.set(page);
    this.failed.set(false);
    this.busy.set(false);
  }
}
