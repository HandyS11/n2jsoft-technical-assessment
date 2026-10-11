import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { confirmDeletion } from './confirm-deletion';
import { ExpenseReportDetails } from './expense-report-details';
import { ExpenseReportsApi } from './expense-reports.api';

function isNotFound(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 404;
}

@Component({
  selector: 'app-expense-report-page',
  imports: [DatePipe, RouterLink],
  template: `
    <a routerLink="/expense-reports" class="underline">← Notes de frais</a>
    @if (error(); as message) {
      <p role="alert">{{ message }}</p>
    }
    @if (report(); as report) {
      <h1 class="my-4 text-2xl font-semibold">{{ report.title }}</h1>
      <p>Mois : {{ firstDay() | date: 'MMMM y' }}</p>
      <button type="button" class="mt-4" [disabled]="deleting()" (click)="remove(report)">Supprimer</button>
    } @else if (!error()) {
      <p>Chargement…</p>
    }
  `,
})
export default class ExpenseReportPage {
  private readonly api = inject(ExpenseReportsApi);
  private readonly router = inject(Router);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly report = signal<ExpenseReportDetails | undefined>(undefined);
  protected readonly error = signal<string | null>(null);
  protected readonly deleting = signal(false);
  /** The DatePipe formats a date: the first day of the report's month, in local time. */
  protected readonly firstDay = computed(() => {
    const report = this.report();
    return report ? new Date(report.year, report.month - 1, 1) : null;
  });

  constructor() {
    this.api.get(this.id).subscribe({
      next: (report) => this.report.set(report),
      error: (error: unknown) =>
        this.error.set(
          isNotFound(error) ? 'Note de frais introuvable.' : 'Impossible de charger la note de frais.'
        ),
    });
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
}
