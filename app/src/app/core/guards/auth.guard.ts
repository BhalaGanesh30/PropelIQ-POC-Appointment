import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TokenStorageService } from '../services/token-storage.service';

/**
 * Functional route guard protecting authenticated routes.
 * Checks the `TokenStorageService` signal; if the token is absent or expired
 * the user is redirected to /login (US_001 Edge Case).
 */
export const authGuard: CanActivateFn = () => {
  const tokenStorage = inject(TokenStorageService);
  const router = inject(Router);

  if (tokenStorage.isAuthenticated() && !tokenStorage.isTokenExpired()) {
    return true;
  }

  return router.parseUrl('/login');
};
