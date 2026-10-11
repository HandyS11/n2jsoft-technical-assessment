import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, EMPTY, Subject, switchMap } from 'rxjs';
import { pagedList } from '../../core/paged-list';
import { PaginationComponent } from '../../core/pagination.component';
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
const monthPattern = /^(20\d{2}|2100)-(0[1-9]|1[0-2])$/;

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

function isNotFound(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 404;
}

function createErrorMessage(error: unknown): string {
  const ruleMessage = ruleMessages.get(businessRuleCode(error) ?? '');
  if (ruleMessage !== undefined) {
    return ruleMessage;
  }
  if (isNotFound(error)) {
    // The user was deleted since the active users were loaded.
    return 'Utilisateur introuvable.';
  }
  return "L'opération a échoué. Veuillez réessayer.";
}

/** The chosen user can no longer get a report (deleted or deactivated since the active users were loaded). */
function isUnavailableUser(error: unknown): boolean {
  return isNotFound(error) || businessRuleCode(error) === 'expense_report.inactive_user';
}

@Component({
  selector: 'app-expense-report-list-page',
  imports: [CurrencyPipe, PaginationComponent, ReactiveFormsModule, RouterLink],
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

    @if (list.failed()) {
      <p role="alert">Impossible de charger les notes de frais.</p>
    }
    @if (list.actionFailed()) {
      <p role="alert">L'opération a échoué. Veuillez réessayer.</p>
    }
    @if (list.page(); as page) {
      @if (page.items.length > 0) {
        <table class="w-full text-left">
          <thead>
            <tr>
              <th>Intitulé</th>
              <th>Dépenses</th>
              <th>Total</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (report of page.items; track report.id) {
              <tr>
                <td>
                  <a [routerLink]="['/expense-reports', report.id]" class="underline">{{ report.title }}</a>
                </td>
                <td>{{ report.expenseCount }}</td>
                <td>{{ report.total | currency: 'EUR' }}</td>
                <td>
                  <button type="button" [disabled]="list.busy()" (click)="remove(report)">Supprimer</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
        <app-pagination [list]="list" />
      } @else {
        <p>Aucune note de frais.</p>
      }
    } @else if (!list.failed()) {
      <p>Chargement…</p>
    }
  `,
})
export default class ExpenseReportListPage {
  private readonly reportsApi = inject(ExpenseReportsApi);
  private readonly usersApi = inject(UsersApi);
  private readonly router = inject(Router);

  protected readonly list = pagedList((page) => this.reportsApi.list(page, pageSize));

  /** Undefined while they load the first time; a reload keeps the list shown until it answers. */
  protected readonly activeUsers = signal<UserSummary[] | undefined>(undefined);
  protected readonly usersFailed = signal(false);
  private readonly userLoads = new Subject<void>();
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    userId: ['', Validators.required],
    month: [currentMonth(), [Validators.required, Validators.pattern(monthPattern)]],
  });

  constructor() {
    this.userLoads
      .pipe(
        // Only the latest load counts: an answer arriving late must not replace the users loaded since.
        switchMap(() =>
          this.usersApi.listAllActive().pipe(
            catchError(() => {
              this.usersFailed.set(true);
              return EMPTY;
            })
          )
        ),
        takeUntilDestroyed()
      )
      .subscribe((users) => this.showUsers(users));
    this.userLoads.next();
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
          if (isUnavailableUser(error)) {
            this.userLoads.next();
          }
        },
      });
  }

  protected remove(report: ExpenseReportSummary): void {
    if (confirmDeletion(report.title)) {
      this.list.run(this.reportsApi.delete(report.id));
    }
  }

  private showUsers(users: UserSummary[]): void {
    this.activeUsers.set(users);
    this.usersFailed.set(false);
    // The chosen user is gone from the list: the <select> would show another one than the form holds.
    const userId = this.form.controls.userId;
    if (userId.value !== '' && !users.some((user) => user.id === userId.value)) {
      userId.setValue('');
    }
  }
}
