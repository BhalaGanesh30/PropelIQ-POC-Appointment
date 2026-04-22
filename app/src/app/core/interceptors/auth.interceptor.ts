import {
  HttpErrorResponse,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../../features/auth/services/auth.service';
import { TokenStorageService } from '../services/token-storage.service';

/** Module-level flag prevents concurrent refresh races. */
let isRefreshing = false;

/**
 * Functional HTTP interceptor that:
 * 1. Attaches `Authorization: Bearer <token>` to every non-auth request.
 * 2. On 401, transparently refreshes the access token (AC-2) and retries.
 * 3. On refresh failure, clears tokens and redirects to /login (AC-4).
 *
 * Auth endpoints (login, register, refresh) are excluded to avoid loops.
 */
export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const tokenStorage = inject(TokenStorageService);
  const authService = inject(AuthService);
  const router = inject(Router);

  // Skip token attachment for public auth endpoints.
  if (isAuthEndpoint(req.url)) {
    return next(req);
  }

  const accessToken = tokenStorage.getAccessToken();
  const authReq = accessToken ? addBearerToken(req, accessToken) : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // AC-2: 401 → attempt silent token refresh once.
      if (error.status === 401 && !isRefreshing) {
        const refreshToken = tokenStorage.getRefreshToken();
        const expiredToken = tokenStorage.getAccessToken();

        if (refreshToken && expiredToken) {
          isRefreshing = true;
          return authService
            .refresh({ accessToken: expiredToken, refreshToken })
            .pipe(
              switchMap((response) => {
                isRefreshing = false;
                tokenStorage.saveTokens(
                  response.accessToken,
                  response.refreshToken
                );
                return next(addBearerToken(req, response.accessToken));
              }),
              catchError((refreshError) => {
                isRefreshing = false;
                tokenStorage.clearTokens();
                router.navigateByUrl('/login');
                return throwError(() => refreshError);
              })
            );
        }

        // No tokens to refresh — redirect to login.
        tokenStorage.clearTokens();
        router.navigateByUrl('/login');
      }

      return throwError(() => error);
    })
  );
};

function isAuthEndpoint(url: string): boolean {
  return (
    url.includes('/auth/login') ||
    url.includes('/auth/register') ||
    url.includes('/auth/refresh') ||
    url.includes('/auth/send-otp') ||
    url.includes('/auth/verify-otp') ||
    url.includes('/auth/confirm-email')
  );
}

function addBearerToken(
  req: HttpRequest<unknown>,
  token: string
): HttpRequest<unknown> {
  return req.clone({
    headers: req.headers.set('Authorization', `Bearer ${token}`),
  });
}
