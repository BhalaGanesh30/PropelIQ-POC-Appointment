import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Functional route guard protecting authenticated routes.
 * When unauthenticated, stores the attempted URL for post-login redirect
 * and navigates to /login (US_001 Edge Case).
 *
 * Replace stub check with real token validation in EP-001.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Preserve attempted URL for post-authentication redirect.
  authService.redirectUrl = state.url;
  return router.createUrlTree(['/login']);
};
