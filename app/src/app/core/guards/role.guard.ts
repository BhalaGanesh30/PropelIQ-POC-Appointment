import { inject } from '@angular/core';
import { CanActivateFn, Router, ActivatedRouteSnapshot } from '@angular/router';
import { TokenStorageService } from '../services/token-storage.service';

/**
 * Functional route guard that restricts access to routes whose
 * `data.roles` array contains the current user's role.
 *
 * Usage in route config:
 * ```ts
 * {
 *   path: 'staff/insurance/report',
 *   canActivate: [authGuard, roleGuard],
 *   data: { roles: ['Staff', 'Admin'] },
 * }
 * ```
 *
 * On failure:
 * - Unauthenticated users → redirected to /login (handled by authGuard first).
 * - Authenticated users without the required role → redirected to /forbidden.
 *   (Edge Case 2 EP-005 US_039: Patient role receives 403 equivalent.)
 */
export const roleGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const tokenStorage = inject(TokenStorageService);
  const router = inject(Router);

  const allowedRoles: string[] = (route.data?.['roles'] as string[]) ?? [];
  if (allowedRoles.length === 0) {
    // No role restriction defined — allow through.
    return true;
  }

  const userRole = tokenStorage.getUserRole();
  if (userRole && allowedRoles.includes(userRole)) {
    return true;
  }

  // Authenticated but wrong role → /forbidden (Edge Case 2).
  return router.parseUrl('/forbidden');
};
