import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, EMPTY, Observable, Subject, switchMap } from 'rxjs';
import { PagedResult, pageCountOf } from '../../core/paged-result';
import { UserSummary } from './user-summary';
import { UsersApi } from './users.api';

const pageSize = 20;

@Component({
  selector: 'app-user-list-page',
  imports: [RouterLink],
  template: `
    <div class="mb-4 flex items-center justify-between">
      <h1 class="text-2xl font-semibold">Utilisateurs</h1>
      <a routerLink="/users/new" class="underline">Nouvel utilisateur</a>
    </div>
    @if (failed()) {
      <p role="alert">Impossible de charger les utilisateurs.</p>
    }
    @if (actionFailed()) {
      <p role="alert">L'opération a échoué. Veuillez réessayer.</p>
    }
    @if (page(); as page) {
      @if (page.items.length > 0) {
        <table class="w-full text-left">
          <thead>
            <tr>
              <th>Nom</th>
              <th>Ville</th>
              <th>Quota mensuel</th>
              <th>Statut</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (user of page.items; track user.id) {
              <tr>
                <td>{{ user.name }}</td>
                <td>{{ user.city }}</td>
                <td>{{ user.monthlyExpenseQuota }}</td>
                <td>{{ user.isActive ? 'Actif' : 'Inactif' }}</td>
                <td class="flex gap-3">
                  <a [routerLink]="['/users', user.id, 'edit']" class="underline">Modifier</a>
                  <button type="button" [disabled]="busy()" (click)="toggle(user)">
                    {{ user.isActive ? 'Désactiver' : 'Activer' }}
                  </button>
                  <button type="button" [disabled]="busy()" (click)="remove(user)">Supprimer</button>
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
        <p>Aucun utilisateur.</p>
      }
    } @else if (!failed()) {
      <p>Chargement…</p>
    }
  `,
})
export default class UserListPage {
  private readonly api = inject(UsersApi);
  protected readonly page = signal<PagedResult<UserSummary> | undefined>(undefined);
  protected readonly failed = signal(false);
  protected readonly actionFailed = signal(false);
  /** An action runs, until the page reloaded after it shows: a second click would act on a stale row. */
  protected readonly busy = signal(false);
  protected readonly pageCount = computed(() => {
    const page = this.page();
    return page ? pageCountOf(page) : 1;
  });

  private readonly pageRequests = new Subject<number>();

  constructor() {
    this.pageRequests
      .pipe(
        // Only the latest request counts: an answer arriving late must not replace the page asked for since.
        switchMap((pageNumber) =>
          this.api.list(pageNumber, pageSize).pipe(
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

  private show(page: PagedResult<UserSummary>): void {
    // Users were deleted past the page shown: show the last page left rather than an empty one. Always an earlier
    // page, so this ends even if more users go meanwhile.
    const lastPage = pageCountOf(page);
    if (page.items.length === 0 && page.page > lastPage) {
      this.load(lastPage);
      return;
    }
    this.page.set(page);
    this.failed.set(false);
    this.busy.set(false);
  }

  protected toggle(user: UserSummary): void {
    this.run(user.isActive ? this.api.deactivate(user.id) : this.api.activate(user.id));
  }

  protected remove(user: UserSummary): void {
    if (window.confirm(`Supprimer l'utilisateur ${user.name} ?`)) {
      this.run(this.api.delete(user.id));
    }
  }

  private run(action: Observable<void>): void {
    this.busy.set(true);
    this.actionFailed.set(false);
    action.subscribe({
      next: () => this.load(this.page()?.page ?? 1),
      error: () => {
        // The user may be gone (deleted in another tab): reload so a stale row does not stay.
        this.actionFailed.set(true);
        this.load(this.page()?.page ?? 1);
      },
    });
  }
}
