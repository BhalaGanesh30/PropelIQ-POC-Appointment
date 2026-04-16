# Task - TASK_002

## Requirement Reference

- User Story: us_014
- Story Location: .propel/context/tasks/EP-001/us_014/us_014.md
- Acceptance Criteria:
  - AC-1: Given I am on the login page, When I enter valid credentials and submit, Then the system validates my credentials, issues a JWT access token and a refresh token, and redirects me to the role-appropriate dashboard within 500 ms.
  - AC-2: Given I am authenticated, When my access token expires, Then the system uses the refresh token to issue a new access token without requiring me to log in again.
  - AC-3: Given I submit invalid credentials, When the login request is processed, Then the system returns a generic "Invalid username or password" message (without distinguishing between wrong username vs. wrong password) and records the failed attempt.
  - AC-4: Given I log out, When the logout request is processed, Then the current JWT and refresh token are revoked server-side and I am redirected to the login page.
- Edge Cases:
  - What happens if I try to use a revoked refresh token? System rejects the token with HTTP 401 and logs the suspicious activity.
  - How does the system handle login from an unrecognized IP or device? Login proceeds; anomaly is flagged for future alerting but does not block access in Phase 1.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-002-login.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | figma_spec.md#SCR-002 |
| **UXR Requirements** | UXR-201, UXR-202, UXR-205, UXR-301, UXR-501 |
| **Design Tokens** | designsystem.md — colors, typography, spacing |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | N/A (consumed via API) | N/A |
| Database | N/A | N/A |
| Library | Angular Material | 17.x |
| Library | Angular Reactive Forms | 17.x (bundled) |
| Library | @angular/router | 17.x (bundled) |
| Library | rxjs | 7.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Build the Angular 17 login page component and the client-side authentication infrastructure including JWT token storage, automatic token refresh via HTTP interceptor, route guards, and role-based dashboard redirect. The login page implements SCR-002 specifications: email/password fields with inline validation, remember-me option, forgot-password and register links, account lockout banner, and loading spinner on submit (UXR-501). The `AuthInterceptor` attaches the bearer token to all API requests and transparently refreshes expired tokens using the refresh endpoint (AC-2). The `AuthGuard` protects routes by checking authentication state and redirects unauthenticated users to `/auth/login`. Token storage uses `localStorage` (or `sessionStorage` when remember-me is unchecked) for the access/refresh token pair. Logout clears local storage and calls the server-side revocation endpoint (AC-4).

## Dependent Tasks

- US_001 tasks (requires Angular scaffold, routing shell, Material theme)
- US_013 task_002 (requires auth feature module, auth routing, AuthService shell)
- task_001_be_jwt_authentication (requires backend login, refresh, logout endpoints)

## Impacted Components

- New: `client/src/app/features/auth/pages/login/login.component.ts` (login page component)
- New: `client/src/app/features/auth/pages/login/login.component.html` (login template)
- New: `client/src/app/features/auth/pages/login/login.component.scss` (login styles)
- New: `client/src/app/core/interceptors/auth.interceptor.ts` (JWT bearer interceptor with refresh)
- New: `client/src/app/core/guards/auth.guard.ts` (route authentication guard)
- New: `client/src/app/core/services/token-storage.service.ts` (token persistence)
- Modify: `client/src/app/features/auth/services/auth.service.ts` (add login, refresh, logout methods)
- Modify: `client/src/app/features/auth/auth-routing.module.ts` (add login route)
- Modify: `client/src/app/app.config.ts` (register interceptor via `provideHttpClient(withInterceptors(...))`)

## Implementation Plan

1. **Create `TokenStorageService`** for managing JWT and refresh token persistence. Supports `localStorage` (remember-me) or `sessionStorage` (session-only):

```typescript
// client/src/app/core/services/token-storage.service.ts
import { Injectable, signal } from '@angular/core';

const ACCESS_TOKEN_KEY = 'propeliq_access_token';
const REFRESH_TOKEN_KEY = 'propeliq_refresh_token';

@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private storage: Storage = localStorage;
  isAuthenticated = signal(false);

  constructor() {
    this.isAuthenticated.set(!!this.getAccessToken());
  }

  useSessionStorage(): void {
    this.storage = sessionStorage;
  }

  useLocalStorage(): void {
    this.storage = localStorage;
  }

  saveTokens(accessToken: string, refreshToken: string): void {
    this.storage.setItem(ACCESS_TOKEN_KEY, accessToken);
    this.storage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    this.isAuthenticated.set(true);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY)
      ?? sessionStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY)
      ?? sessionStorage.getItem(REFRESH_TOKEN_KEY);
  }

  clearTokens(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    this.isAuthenticated.set(false);
  }

  getDecodedToken(): Record<string, unknown> | null {
    const token = this.getAccessToken();
    if (!token) return null;
    try {
      const payload = token.split('.')[1];
      return JSON.parse(atob(payload));
    } catch {
      return null;
    }
  }

  getUserRole(): string | null {
    const decoded = this.getDecodedToken();
    if (!decoded) return null;
    const role = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    return Array.isArray(role) ? role[0] : (role as string) ?? null;
  }

  isTokenExpired(): boolean {
    const decoded = this.getDecodedToken();
    if (!decoded?.['exp']) return true;
    const expiry = (decoded['exp'] as number) * 1000;
    return Date.now() >= expiry;
  }
}
```

2. **Create the login component** implementing SCR-002 layout with email/password fields, remember-me checkbox, inline validation, and loading state:

```typescript
// client/src/app/features/auth/pages/login/login.component.ts
import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../services/auth.service';
import { TokenStorageService } from '../../../../core/services/token-storage.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatIconModule,
    RouterLink,
  ],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  loginForm = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
    password: new FormControl('', [Validators.required]),
    rememberMe: new FormControl(false),
  });

  isSubmitting = signal(false);
  serverError = signal<string | null>(null);
  showPassword = signal(false);

  constructor(
    private authService: AuthService,
    private tokenStorage: TokenStorageService,
    private router: Router,
  ) {}

  togglePasswordVisibility(): void {
    this.showPassword.update(v => !v);
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isSubmitting.set(true);
    this.serverError.set(null);

    const { email, password, rememberMe } = this.loginForm.value;

    // Configure storage based on remember-me (AC-1)
    if (rememberMe) {
      this.tokenStorage.useLocalStorage();
    } else {
      this.tokenStorage.useSessionStorage();
    }

    this.authService.login({ email: email!, password: password! }).subscribe({
      next: (response) => {
        this.tokenStorage.saveTokens(
          response.accessToken, response.refreshToken);
        this.isSubmitting.set(false);
        // Redirect to role-appropriate dashboard (AC-1)
        this.router.navigateByUrl(response.redirectUrl ?? '/dashboard');
      },
      error: (err) => {
        this.isSubmitting.set(false);
        if (err.status === 401) {
          // AC-3: Generic error — do not distinguish cause
          this.serverError.set(
            err.error?.detail ?? 'Invalid username or password');
        } else if (err.status === 429) {
          this.serverError.set(
            'Too many login attempts. Please try again later.');
        } else {
          this.serverError.set('An unexpected error occurred. Please try again.');
        }
      },
    });
  }
}
```

3. **Create the login template** with SCR-002 layout, inline errors with `aria-describedby` (UXR-205), and lockout banner:

```html
<!-- client/src/app/features/auth/pages/login/login.component.html -->
<div class="login-container" role="main" aria-label="User Login">
  <header class="login-header">
    <img src="assets/logo.svg" alt="PropelIQ Logo" class="logo" />
    <h1>Welcome Back</h1>
  </header>

  <form [formGroup]="loginForm" (ngSubmit)="onSubmit()" novalidate>
    @if (serverError()) {
      <div class="server-error" role="alert" aria-live="polite">
        {{ serverError() }}
      </div>
    }

    <mat-form-field appearance="outline" class="full-width">
      <mat-label>Email Address</mat-label>
      <input matInput formControlName="email" type="email"
             autocomplete="email"
             [attr.aria-describedby]="loginForm.controls.email.invalid
               && loginForm.controls.email.touched ? 'email-error' : null" />
      @if (loginForm.controls.email.touched
           && loginForm.controls.email.invalid) {
        <mat-error id="email-error">
          @if (loginForm.controls.email.errors?.['required']) {
            Email is required
          }
          @if (loginForm.controls.email.errors?.['email']) {
            Please enter a valid email address
          }
        </mat-error>
      }
    </mat-form-field>

    <mat-form-field appearance="outline" class="full-width">
      <mat-label>Password</mat-label>
      <input matInput formControlName="password"
             [type]="showPassword() ? 'text' : 'password'"
             autocomplete="current-password"
             [attr.aria-describedby]="loginForm.controls.password.invalid
               && loginForm.controls.password.touched
               ? 'password-error' : null" />
      <button mat-icon-button matSuffix type="button"
              (click)="togglePasswordVisibility()"
              [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'">
        <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
      </button>
      @if (loginForm.controls.password.touched
           && loginForm.controls.password.invalid) {
        <mat-error id="password-error">
          Password is required
        </mat-error>
      }
    </mat-form-field>

    <div class="form-options">
      <mat-checkbox formControlName="rememberMe" color="primary">
        Remember me
      </mat-checkbox>
      <a routerLink="/auth/forgot-password" class="forgot-link">
        Forgot password?
      </a>
    </div>

    <button mat-raised-button color="primary" type="submit"
            [disabled]="loginForm.invalid || isSubmitting()"
            class="submit-btn">
      @if (isSubmitting()) {
        <mat-spinner diameter="20"></mat-spinner>
      } @else {
        Log In
      }
    </button>
  </form>

  <p class="register-link">
    Don't have an account? <a routerLink="/auth/register">Register</a>
  </p>

  <footer class="login-footer">
    <a href="/terms" target="_blank" rel="noopener">Terms of Service</a>
    <a href="/privacy" target="_blank" rel="noopener">Privacy Policy</a>
  </footer>
</div>
```

4. **Create responsive SCSS** matching SCR-002 layout — single-column centered, max-width 480px, branded header:

```scss
// client/src/app/features/auth/pages/login/login.component.scss
.login-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-height: 100vh;
  padding: 24px 16px;
  max-width: 480px;
  margin: 0 auto;
}

.login-header {
  text-align: center;
  margin-bottom: 32px;

  .logo {
    height: 48px;
    margin-bottom: 16px;
  }

  h1 {
    font-size: 1.5rem;
    font-weight: 600;
    margin: 0;
  }
}

.full-width {
  width: 100%;
  margin-bottom: 8px;
}

.form-options {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  width: 100%;
}

.forgot-link {
  color: var(--mat-primary-color, #1976d2);
  text-decoration: none;
  font-size: 0.875rem;

  &:hover {
    text-decoration: underline;
  }

  &:focus-visible {
    outline: 2px solid var(--mat-primary-color, #1976d2);
    outline-offset: 2px;
    border-radius: 2px;
  }
}

.submit-btn {
  width: 100%;
  height: 48px;
  font-size: 1rem;
  min-height: 44px; // UXR-304
  min-width: 44px;
}

.server-error {
  color: var(--mat-error-color, #f44336);
  background-color: #fdecea;
  border: 1px solid #f5c6cb;
  border-radius: 4px;
  padding: 12px 16px;
  margin-bottom: 16px;
  font-size: 0.875rem;
  width: 100%;
}

.register-link {
  text-align: center;
  margin-top: 24px;
  font-size: 0.875rem;

  a {
    color: var(--mat-primary-color, #1976d2);
    text-decoration: none;
    font-weight: 500;

    &:focus-visible {
      outline: 2px solid var(--mat-primary-color, #1976d2);
      outline-offset: 2px;
    }
  }
}

.login-footer {
  margin-top: auto;
  padding-top: 32px;
  display: flex;
  gap: 24px;
  font-size: 0.75rem;

  a {
    color: rgba(0, 0, 0, 0.54);
    text-decoration: none;

    &:hover { text-decoration: underline; }

    &:focus-visible {
      outline: 2px solid var(--mat-primary-color, #1976d2);
      outline-offset: 2px;
    }
  }
}

// UXR-301: Responsive breakpoints
@media (max-width: 375px) {
  .login-container { padding: 16px 12px; }
  .login-header h1 { font-size: 1.25rem; }
}

@media (min-width: 768px) {
  .login-container { padding: 48px 24px; }
  .login-header { margin-bottom: 48px; }
}
```

5. **Create the `AuthInterceptor`** as a functional interceptor that attaches the bearer token and transparently handles token refresh (AC-2):

```typescript
// client/src/app/core/interceptors/auth.interceptor.ts
import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { TokenStorageService } from '../services/token-storage.service';
import { AuthService } from '../../features/auth/services/auth.service';

let isRefreshing = false;

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
) => {
  const tokenStorage = inject(TokenStorageService);
  const authService = inject(AuthService);
  const router = inject(Router);

  // Skip auth header for auth endpoints
  if (req.url.includes('/auth/login') ||
      req.url.includes('/auth/register') ||
      req.url.includes('/auth/refresh')) {
    return next(req);
  }

  const accessToken = tokenStorage.getAccessToken();
  let authReq = req;
  if (accessToken) {
    authReq = req.clone({
      headers: req.headers.set('Authorization', `Bearer ${accessToken}`),
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // AC-2: Transparent token refresh on 401
      if (error.status === 401 && !isRefreshing) {
        isRefreshing = true;
        const refreshToken = tokenStorage.getRefreshToken();
        const expiredToken = tokenStorage.getAccessToken();

        if (refreshToken && expiredToken) {
          return authService.refresh({
            accessToken: expiredToken,
            refreshToken,
          }).pipe(
            switchMap((response) => {
              isRefreshing = false;
              tokenStorage.saveTokens(
                response.accessToken, response.refreshToken);
              // Retry original request with new token
              const retryReq = req.clone({
                headers: req.headers.set(
                  'Authorization', `Bearer ${response.accessToken}`),
              });
              return next(retryReq);
            }),
            catchError((refreshError) => {
              isRefreshing = false;
              // Refresh failed — clear tokens and redirect to login
              tokenStorage.clearTokens();
              router.navigateByUrl('/auth/login');
              return throwError(() => refreshError);
            }),
          );
        }

        // No refresh token — redirect to login
        tokenStorage.clearTokens();
        router.navigateByUrl('/auth/login');
      }

      return throwError(() => error);
    }),
  );
};
```

6. **Create the `AuthGuard`** as a functional `CanActivateFn` that protects routes:

```typescript
// client/src/app/core/guards/auth.guard.ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TokenStorageService } from '../services/token-storage.service';

export const authGuard: CanActivateFn = () => {
  const tokenStorage = inject(TokenStorageService);
  const router = inject(Router);

  if (tokenStorage.isAuthenticated() && !tokenStorage.isTokenExpired()) {
    return true;
  }

  // Redirect to login if not authenticated
  return router.parseUrl('/auth/login');
};
```

7. **Update `AuthService`** with login, refresh, and logout methods:

```typescript
// Add to client/src/app/features/auth/services/auth.service.ts
login(request: LoginRequest): Observable<LoginResponse> {
  return this.http.post<LoginResponse>(
    `${this.apiUrl}/login`, request);
}

refresh(request: RefreshRequest): Observable<LoginResponse> {
  return this.http.post<LoginResponse>(
    `${this.apiUrl}/refresh`, request);
}

logout(): Observable<void> {
  const refreshToken = inject(TokenStorageService).getRefreshToken();
  return this.http.post<void>(
    `${this.apiUrl}/logout`, { refreshToken });
}
```

```typescript
// Add to models/register.model.ts
export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  redirectUrl?: string;
}

export interface RefreshRequest {
  accessToken: string;
  refreshToken: string;
}
```

8. **Register the interceptor and guard** in the application configuration:

```typescript
// In app.config.ts
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimations(),
  ],
};
```

Route configuration:
```typescript
// In auth-routing.module.ts — add login route
{
  path: 'login',
  loadComponent: () => import('./pages/login/login.component')
    .then(m => m.LoginComponent),
  title: 'Log In — PropelIQ'
}
```

Protected routes use the guard:
```typescript
// In app-routing.module.ts
{
  path: 'dashboard',
  canActivate: [authGuard],
  loadComponent: () => import('./features/dashboard/dashboard.component')
    .then(m => m.DashboardComponent),
}
```

## Current Project State

```text
propelIQ/
├── client/
│   └── src/
│       ├── app/
│       │   ├── app.config.ts           (from US_001)
│       │   ├── app-routing.module.ts   (from US_001)
│       │   ├── core/
│       │   │   ├── interceptors/
│       │   │   ├── guards/
│       │   │   └── services/
│       │   ├── shared/
│       │   └── features/
│       │       └── auth/
│       │           ├── auth-routing.module.ts
│       │           ├── pages/
│       │           │   └── register/   (from US_013 task_002)
│       │           ├── services/
│       │           │   └── auth.service.ts  (from US_013 task_002)
│       │           └── models/
│       │               └── register.model.ts (from US_013 task_002)
│       ├── assets/
│       ├── environments/
│       └── styles/
└── server/
    └── src/
        └── PropelIQ.Api/
            └── Controllers/
                └── AuthController.cs    (from task_001)
```

> Placeholder: Update on execution based on US_001, US_013 task_002, and task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/auth/pages/login/login.component.ts | Login page with email/password form, remember-me, loading state |
| CREATE | client/src/app/features/auth/pages/login/login.component.html | SCR-002 template with inline validation, aria-describedby, lockout banner |
| CREATE | client/src/app/features/auth/pages/login/login.component.scss | Responsive styles for 375px/768px/1440px breakpoints |
| CREATE | client/src/app/core/interceptors/auth.interceptor.ts | Functional interceptor attaching bearer token with transparent refresh |
| CREATE | client/src/app/core/guards/auth.guard.ts | CanActivateFn redirecting unauthenticated users to login |
| CREATE | client/src/app/core/services/token-storage.service.ts | Token persistence with localStorage/sessionStorage and JWT decode |
| MODIFY | client/src/app/features/auth/services/auth.service.ts | Add login(), refresh(), logout() methods |
| MODIFY | client/src/app/features/auth/models/register.model.ts | Add LoginRequest, LoginResponse, RefreshRequest interfaces |
| MODIFY | client/src/app/features/auth/auth-routing.module.ts | Add /login route with lazy-loaded component |
| MODIFY | client/src/app/app.config.ts | Register authInterceptor via provideHttpClient(withInterceptors(...)) |

## External References

- Angular functional interceptors: https://angular.dev/guide/http/interceptors
- Angular functional route guards: https://angular.dev/guide/routing/route-guards
- Angular signals: https://angular.dev/guide/signals
- Angular reactive forms: https://angular.dev/guide/forms/reactive-forms
- Angular Material form field: https://material.angular.io/components/form-field/overview
- Angular Material checkbox: https://material.angular.io/components/checkbox/overview
- JWT structure: https://jwt.io/introduction
- OWASP session management: https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve with hot reload
ng serve --open

# Navigate to login
# http://localhost:4200/auth/login

# Run unit tests
ng test --watch=false

# Lint
ng lint
```

## Implementation Validation Strategy

- [ ] Login form renders with email, password, remember-me, and submit button per SCR-002
- [ ] Valid credentials trigger API call, store tokens, and redirect to role-appropriate dashboard (AC-1)
- [ ] Invalid credentials display generic "Invalid username or password" error (AC-3)
- [ ] Account lockout displays lockout message with retry guidance
- [ ] Password show/hide toggle works with correct aria-label
- [ ] Interceptor attaches `Authorization: Bearer <token>` header to API requests
- [ ] Interceptor transparently refreshes expired access token using refresh endpoint (AC-2)
- [ ] Failed refresh redirects to login page with cleared tokens
- [ ] AuthGuard blocks unauthenticated access and redirects to `/auth/login`
- [ ] Logout clears token storage and calls server-side revocation (AC-4)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

## Implementation Checklist

- [ ] Create `TokenStorageService` with `saveTokens`, `getAccessToken`, `getRefreshToken`, `clearTokens`, `getUserRole`, `isTokenExpired`
- [ ] Create `LoginComponent` with reactive form, remember-me checkbox, password show/hide toggle, loading spinner, and generic error display
- [ ] Create responsive login SCSS matching SCR-002 single-column centered layout with 375/768/1440px breakpoints
- [ ] Create `authInterceptor` functional interceptor that attaches bearer token and handles 401 with transparent refresh
- [ ] Create `authGuard` functional CanActivateFn redirecting unauthenticated users to `/auth/login`
- [ ] Update `AuthService` with `login()`, `refresh()`, and `logout()` methods calling backend endpoints
- [ ] Register interceptor in `app.config.ts` via `provideHttpClient(withInterceptors([authInterceptor]))`
- [ ] **[UI Tasks - MANDATORY]** Reference wireframe from Design References table during implementation
