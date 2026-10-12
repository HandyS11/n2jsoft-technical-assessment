import { Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { pagedList } from '../../core/paged-list';
import { PaginationComponent } from '../../core/pagination.component';
import { UserSummary } from './user-summary';
import { UsersApi } from './users.api';

const pageSize = 20;

/** The "Statut" filter: the `isActive` the API is asked for. */
const statusFilters = { all: undefined, active: true, inactive: false } as const;
type StatusFilter = keyof typeof statusFilters;

@Component({
  selector: 'app-user-list-page',
  imports: [PaginationComponent, ReactiveFormsModule, RouterLink],
  template: `
    <div class="mb-6 flex flex-wrap items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold text-slate-900">Utilisateurs</h1>
      <div class="flex items-center gap-3">
        <label for="status">Statut</label>
        <select id="status" [formControl]="status">
          <option value="all">Tous</option>
          <option value="active">Actifs</option>
          <option value="inactive">Inactifs</option>
        </select>
        <a routerLink="/users/new" class="btn btn-primary">Nouvel utilisateur</a>
      </div>
    </div>
    @if (list.failed()) {
      <p role="alert" class="mb-4">Impossible de charger les utilisateurs.</p>
    }
    @if (list.actionFailed()) {
      <p role="alert" class="mb-4">L'opération a échoué. Veuillez réessayer.</p>
    }
    @if (list.page(); as page) {
      @if (page.items.length > 0) {
        <div class="card overflow-x-auto">
          <table>
            <thead>
              <tr>
                <th>Nom</th>
                <th>Ville</th>
                <th class="text-right">Quota mensuel</th>
                <th>Statut</th>
                <th class="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (user of page.items; track user.id) {
                <tr>
                  <td class="font-medium text-slate-900">{{ user.name }}</td>
                  <td>{{ user.city }}</td>
                  <td class="text-right tabular-nums">{{ user.monthlyExpenseQuota }}</td>
                  <td>
                    @if (user.isActive) {
                      <span class="badge bg-green-50 text-green-700 ring-green-600/20">Actif</span>
                    } @else {
                      <span class="badge bg-slate-100 text-slate-600 ring-slate-500/20">Inactif</span>
                    }
                  </td>
                  <td>
                    <div class="flex justify-end gap-2">
                      <a [routerLink]="['/users', user.id, 'edit']" class="btn btn-sm">Modifier</a>
                      <button type="button" class="btn-sm" [disabled]="list.busy()" (click)="toggle(user)">
                        {{ user.isActive ? 'Désactiver' : 'Activer' }}
                      </button>
                      <button type="button" class="btn-sm btn-danger" [disabled]="list.busy()" (click)="remove(user)">
                        Supprimer
                      </button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <app-pagination [list]="list" />
      } @else {
        <p class="card muted p-8 text-center">Aucun utilisateur.</p>
      }
    } @else if (!list.failed()) {
      <p class="muted">Chargement…</p>
    }
  `,
})
export default class UserListPage {
  private readonly api = inject(UsersApi);
  /** Before the list: its first page is fetched as soon as it is created. */
  protected readonly status = inject(NonNullableFormBuilder).control<StatusFilter>('all');
  protected readonly list = pagedList((page) => this.api.list(page, pageSize, statusFilters[this.status.value]));

  constructor() {
    // Other users to show: start again from their first page.
    this.status.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.list.load(1));
  }

  protected toggle(user: UserSummary): void {
    this.list.run(user.isActive ? this.api.deactivate(user.id) : this.api.activate(user.id));
  }

  protected remove(user: UserSummary): void {
    if (window.confirm(`Supprimer l'utilisateur ${user.name} ?`)) {
      this.list.run(this.api.delete(user.id));
    }
  }
}
