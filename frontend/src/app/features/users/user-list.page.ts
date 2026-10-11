import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { pagedList } from '../../core/paged-list';
import { PaginationComponent } from '../../core/pagination.component';
import { UserSummary } from './user-summary';
import { UsersApi } from './users.api';

const pageSize = 20;

@Component({
  selector: 'app-user-list-page',
  imports: [PaginationComponent, RouterLink],
  template: `
    <div class="mb-4 flex items-center justify-between">
      <h1 class="text-2xl font-semibold">Utilisateurs</h1>
      <a routerLink="/users/new" class="underline">Nouvel utilisateur</a>
    </div>
    @if (list.failed()) {
      <p role="alert">Impossible de charger les utilisateurs.</p>
    }
    @if (list.actionFailed()) {
      <p role="alert">L'opération a échoué. Veuillez réessayer.</p>
    }
    @if (list.page(); as page) {
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
                  <button type="button" [disabled]="list.busy()" (click)="toggle(user)">
                    {{ user.isActive ? 'Désactiver' : 'Activer' }}
                  </button>
                  <button type="button" [disabled]="list.busy()" (click)="remove(user)">Supprimer</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
        <app-pagination [list]="list" />
      } @else {
        <p>Aucun utilisateur.</p>
      }
    } @else if (!list.failed()) {
      <p>Chargement…</p>
    }
  `,
})
export default class UserListPage {
  private readonly api = inject(UsersApi);
  protected readonly list = pagedList((page) => this.api.list(page, pageSize));

  protected toggle(user: UserSummary): void {
    this.list.run(user.isActive ? this.api.deactivate(user.id) : this.api.activate(user.id));
  }

  protected remove(user: UserSummary): void {
    if (window.confirm(`Supprimer l'utilisateur ${user.name} ?`)) {
      this.list.run(this.api.delete(user.id));
    }
  }
}
