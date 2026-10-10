import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, EMPTY } from 'rxjs';
import { UsersApi } from './users.api';

@Component({
  selector: 'app-user-list-page',
  template: `
    <h1 class="mb-4 text-2xl font-semibold">Utilisateurs</h1>
    @if (users(); as users) {
      @if (users.length > 0) {
        <ul>
          @for (user of users; track user.id) {
            <li>{{ user.name }}</li>
          }
        </ul>
      } @else {
        <p>Aucun utilisateur.</p>
      }
    } @else if (failed()) {
      <p role="alert">Impossible de charger les utilisateurs.</p>
    } @else {
      <p>Chargement…</p>
    }
  `,
})
export default class UserListPage {
  protected readonly failed = signal(false);
  protected readonly users = toSignal(
    inject(UsersApi)
      .list()
      .pipe(
        catchError(() => {
          this.failed.set(true);
          return EMPTY;
        })
      )
  );
}
