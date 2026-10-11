import { Route } from '@angular/router';

export const appRoutes: Route[] = [
  { path: '', pathMatch: 'full', redirectTo: 'expense-reports' },
  {
    path: 'expense-reports',
    title: 'Notes de frais',
    loadComponent: () => import('./features/expense-reports/expense-report-list.page'),
  },
  {
    path: 'expense-reports/:id',
    title: 'Note de frais',
    loadComponent: () => import('./features/expense-reports/expense-report.page'),
  },
  {
    path: 'users',
    title: 'Utilisateurs',
    loadComponent: () => import('./features/users/user-list.page'),
  },
  {
    path: 'users/new',
    title: 'Nouvel utilisateur',
    loadComponent: () => import('./features/users/user-form.page'),
  },
  {
    path: 'users/:id/edit',
    title: "Modifier l'utilisateur",
    loadComponent: () => import('./features/users/user-form.page'),
  },
  { path: '**', redirectTo: 'expense-reports' },
];
