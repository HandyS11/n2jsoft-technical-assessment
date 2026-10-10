import { Route } from '@angular/router';

export const appRoutes: Route[] = [
  { path: '', pathMatch: 'full', redirectTo: 'users' },
  {
    path: 'users',
    title: 'Utilisateurs',
    loadComponent: () => import('./features/users/user-list.page'),
  },
];
