import { Route } from '@angular/router';

export default [
  {
    path: 'users',
    loadComponent: () =>
      import('./pages/user-management/user-management.component').then(
        (m) => m.UserManagementComponent,
      ),
    title: 'User Management — PropelIQ',
  },
] satisfies Route[];
