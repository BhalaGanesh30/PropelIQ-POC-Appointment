import { Route } from '@angular/router';

export default [
  {
    path: 'register',
    loadComponent: () =>
      import('./pages/register/register.component').then(
        (m) => m.RegisterComponent
      ),
    title: 'Register — PropelIQ',
  },
  {
    path: 'activate',
    loadComponent: () =>
      import('./pages/activate/activate.component').then(
        (m) => m.ActivateComponent
      ),
    title: 'Activate Account — PropelIQ',
  },
  {
    // us_018: step 1 — request reset email (AC-1).
    path: 'forgot-password',
    loadComponent: () =>
      import('./pages/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent
      ),
    title: 'Forgot Password — PropelIQ',
  },
  {
    // us_018: step 2 — submit new password via link from email (AC-2).
    path: 'reset-password',
    loadComponent: () =>
      import('./pages/reset-password/reset-password.component').then(
        (m) => m.ResetPasswordComponent
      ),
    title: 'Reset Password — PropelIQ',
  },
] satisfies Route[];
