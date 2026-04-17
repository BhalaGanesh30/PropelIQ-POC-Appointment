# Task - TASK_002

## Requirement Reference

- User Story: us_018
- Story Location: .propel/context/tasks/EP-001/us_018/us_018.md
- Acceptance Criteria:
  - AC-1: Given I am on the login page and click "Forgot Password", When I enter my registered email address, Then a password reset link is sent to my email within 2 minutes and the system does not confirm whether the email exists (security: same response for registered and unregistered emails).
  - AC-2: Given I receive the password reset email, When I click the link and submit a new password meeting complexity requirements (8+ characters, 1 uppercase, 1 number, 1 special character), Then my password is updated, the reset link is invalidated, and I am redirected to the login page.
  - AC-3: Given I enter an incorrect password 5 times consecutively, When the 5th failed attempt is recorded, Then my account is locked for 30 minutes, all active sessions are invalidated, and I receive an email notification of the lockout.
  - AC-4: Given my account is locked, When 30 minutes elapse, Then the account unlocks automatically and I can attempt login again.
- Edge Cases:
  - What happens if I click a password reset link more than 24 hours after it was issued? Link is expired; system displays "Reset link expired" with an option to request a new one.
  - How does the system handle multiple password reset requests within a short period? Rate limiting: maximum 3 reset requests per 15 minutes per account to prevent email flooding.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-003-password-reset.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | figma_spec.md#SCR-003 |
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

Build the Angular 17 two-step password reset flow (SCR-003) and account lockout UX on the login page (SCR-002). The forgot-password page (`/auth/forgot-password`) displays a single email input with a "Send Reset Link" CTA per SCR-003 Default state. On submission, it calls `POST /api/v1/auth/forgot-password` and shows a generic confirmation message regardless of the response (matching AC-1 security requirement — no email existence confirmation). The reset-password page (`/auth/reset-password`) reads `email` and `token` from query parameters, displays the new password input with show/hide toggle and a confirm-password field, and validates password complexity inline (8+ chars, 1 uppercase, 1 number, 1 special character per AC-2). On valid submission, it calls `POST /api/v1/auth/reset-password` and redirects to `/auth/login` with a success banner. The SCR-003 step indicator shows the two-step flow (1: Enter Email → 2: New Password). Error states handle expired/invalid tokens (edge case: 24-hour expiry) with a "Request new link" option. On the login page, the existing lockout error banner (from US_014 task_002) is enhanced to display "Account locked. Please try again in 30 minutes." when the API returns the lockout response (AC-3). All forms implement WCAG 2.1 AA compliance with keyboard navigation (UXR-202), error-field association via `aria-describedby` (UXR-205), contrast ratios (UXR-201), responsive layout (UXR-301), and loading spinners (UXR-501).

## Dependent Tasks

- US_014 task_002 (requires Angular auth infrastructure: AuthService, auth routing, login page with error handling)
- US_018 task_001 (requires backend forgot-password and reset-password endpoints)

## Impacted Components

- New: `client/src/app/features/auth/pages/forgot-password/forgot-password.component.ts` (email input, send reset link)
- New: `client/src/app/features/auth/pages/forgot-password/forgot-password.component.html` (template with step indicator)
- New: `client/src/app/features/auth/pages/forgot-password/forgot-password.component.scss` (styles matching SCR-003)
- New: `client/src/app/features/auth/pages/reset-password/reset-password.component.ts` (new password form with complexity validation)
- New: `client/src/app/features/auth/pages/reset-password/reset-password.component.html` (template with password fields and validation feedback)
- New: `client/src/app/features/auth/pages/reset-password/reset-password.component.scss` (styles matching SCR-003)
- New: `client/src/app/shared/validators/password-strength.validator.ts` (reusable password complexity validator)
- Modify: `client/src/app/features/auth/services/auth.service.ts` (add forgotPassword and resetPassword methods)
- Modify: `client/src/app/features/auth/auth-routing.module.ts` (add forgot-password and reset-password routes)
- Modify: `client/src/app/features/auth/pages/login/login.component.ts` (add lockout banner handling and "Forgot Password" link routing)
- Modify: `client/src/app/features/auth/pages/login/login.component.html` (add lockout-specific banner message)

## Implementation Plan

1. **Create `PasswordStrengthValidator`** as a reusable reactive form validator for password complexity:

```typescript
// client/src/app/shared/validators/password-strength.validator.ts
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function passwordStrengthValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value as string;
    if (!value) return null;

    const errors: ValidationErrors = {};

    if (value.length < 8) {
      errors['minLength'] = 'Password must be at least 8 characters.';
    }
    if (!/[A-Z]/.test(value)) {
      errors['uppercase'] = 'Password must contain at least one uppercase letter.';
    }
    if (!/[0-9]/.test(value)) {
      errors['digit'] = 'Password must contain at least one number.';
    }
    if (!/[^a-zA-Z0-9]/.test(value)) {
      errors['special'] = 'Password must contain at least one special character.';
    }

    return Object.keys(errors).length > 0
      ? { passwordStrength: errors }
      : null;
  };
}

export function passwordMatchValidator(
  passwordField: string,
  confirmField: string
): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const password = control.get(passwordField)?.value;
    const confirm = control.get(confirmField)?.value;

    if (password && confirm && password !== confirm) {
      control.get(confirmField)?.setErrors({ passwordMismatch: true });
      return { passwordMismatch: true };
    }

    return null;
  };
}
```

2. **Create `ForgotPasswordComponent`** implementing SCR-003 step 1 — email input with generic confirmation (AC-1):

```typescript
// client/src/app/features/auth/pages/forgot-password/forgot-password.component.ts
import {
  Component,
  ChangeDetectionStrategy,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  Validators,
  ReactiveFormsModule,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
    RouterLink,
  ],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForgotPasswordComponent {
  isLoading = signal(false);
  isSubmitted = signal(false);

  forgotForm = new FormGroup({
    email: new FormControl('', [
      Validators.required,
      Validators.email,
    ]),
  });

  constructor(private authService: AuthService) {}

  onSubmit(): void {
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);

    this.authService
      .forgotPassword(this.forgotForm.value.email!)
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          this.isSubmitted.set(true);
        },
        error: () => {
          // Still show success — prevent user enumeration (AC-1)
          this.isLoading.set(false);
          this.isSubmitted.set(true);
        },
      });
  }
}
```

3. **Create forgot-password template** with step indicator and generic confirmation:

```html
<!-- forgot-password.component.html -->
<div class="auth-container">
  <div class="auth-card">
    <div class="auth-header">
      <h1>Reset Password</h1>
      <div class="step-indicator" role="navigation" aria-label="Password reset steps">
        <div
          class="step active"
          [attr.aria-current]="!isSubmitted() ? 'step' : null">
          <span class="step-number">1</span>
          <span class="step-label">Enter Email</span>
        </div>
        <div class="step-connector"></div>
        <div
          class="step"
          [class.active]="isSubmitted()">
          <span class="step-number">2</span>
          <span class="step-label">New Password</span>
        </div>
      </div>
    </div>

    @if (!isSubmitted()) {
      <form
        [formGroup]="forgotForm"
        (ngSubmit)="onSubmit()"
        class="auth-form">
        <p class="instructions">
          Enter the email address associated with your account and we'll send
          you a link to reset your password.
        </p>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email address</mat-label>
          <input
            matInput
            formControlName="email"
            type="email"
            autocomplete="email"
            [attr.aria-describedby]="
              forgotForm.get('email')?.hasError('email')
                ? 'email-error'
                : null
            " />
          <mat-icon matPrefix>email</mat-icon>
          @if (forgotForm.get('email')?.hasError('required')
               && forgotForm.get('email')?.touched) {
            <mat-error id="email-error">Email is required.</mat-error>
          }
          @if (forgotForm.get('email')?.hasError('email')
               && forgotForm.get('email')?.touched) {
            <mat-error id="email-error">
              Enter a valid email address.
            </mat-error>
          }
        </mat-form-field>

        <button
          mat-flat-button
          color="primary"
          type="submit"
          class="full-width submit-btn"
          [disabled]="isLoading()"
          aria-label="Send password reset link">
          @if (isLoading()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            Send Reset Link
          }
        </button>

        <div class="auth-links">
          <a routerLink="/auth/login" class="back-link">
            Back to Login
          </a>
        </div>
      </form>
    } @else {
      <div class="confirmation" role="status" aria-live="polite">
        <mat-icon class="success-icon">mark_email_read</mat-icon>
        <h2>Check Your Email</h2>
        <p>
          If an account with that email exists, a password reset link has
          been sent. Please check your inbox and spam folder.
        </p>
        <p class="note">The link will expire in 24 hours.</p>
        <a
          mat-stroked-button
          routerLink="/auth/login"
          class="full-width"
          aria-label="Return to login page">
          Back to Login
        </a>
      </div>
    }
  </div>
</div>
```

4. **Create `ResetPasswordComponent`** implementing SCR-003 step 2 — new password with complexity validation (AC-2):

```typescript
// client/src/app/features/auth/pages/reset-password/reset-password.component.ts
import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  Validators,
  ReactiveFormsModule,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../services/auth.service';
import {
  passwordStrengthValidator,
  passwordMatchValidator,
} from '../../../../shared/validators/password-strength.validator';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
    RouterLink,
  ],
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResetPasswordComponent implements OnInit {
  isLoading = signal(false);
  showPassword = signal(false);
  showConfirmPassword = signal(false);
  errorMessage = signal<string | null>(null);
  isTokenExpired = signal(false);

  private email = '';
  private token = '';

  resetForm = new FormGroup(
    {
      newPassword: new FormControl('', [
        Validators.required,
        passwordStrengthValidator(),
      ]),
      confirmPassword: new FormControl('', [Validators.required]),
    },
    { validators: passwordMatchValidator('newPassword', 'confirmPassword') }
  );

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';

    if (!this.email || !this.token) {
      this.errorMessage.set('Invalid or missing reset link parameters.');
      this.isTokenExpired.set(true);
    }
  }

  get passwordErrors(): Record<string, string> | null {
    const ctrl = this.resetForm.get('newPassword');
    if (!ctrl?.errors?.['passwordStrength'] || !ctrl.touched) return null;
    return ctrl.errors['passwordStrength'];
  }

  togglePasswordVisibility(): void {
    this.showPassword.update(v => !v);
  }

  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword.update(v => !v);
  }

  onSubmit(): void {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService
      .resetPassword(
        this.email,
        this.token,
        this.resetForm.value.newPassword!
      )
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          this.router.navigate(['/auth/login'], {
            queryParams: { reason: 'password-reset' },
          });
        },
        error: (err) => {
          this.isLoading.set(false);
          if (err.status === 400) {
            this.errorMessage.set(
              'The reset link is invalid or has expired.'
            );
            this.isTokenExpired.set(true);
          } else {
            this.errorMessage.set(
              'An unexpected error occurred. Please try again.'
            );
          }
        },
      });
  }
}
```

5. **Create reset-password template** with password fields, complexity indicators, and expired-token handling:

```html
<!-- reset-password.component.html -->
<div class="auth-container">
  <div class="auth-card">
    <div class="auth-header">
      <h1>Reset Password</h1>
      <div class="step-indicator" role="navigation" aria-label="Password reset steps">
        <div class="step completed">
          <span class="step-number">&#10003;</span>
          <span class="step-label">Enter Email</span>
        </div>
        <div class="step-connector active"></div>
        <div class="step active" aria-current="step">
          <span class="step-number">2</span>
          <span class="step-label">New Password</span>
        </div>
      </div>
    </div>

    @if (isTokenExpired()) {
      <div class="token-expired" role="alert">
        <mat-icon class="error-icon">link_off</mat-icon>
        <h2>Reset Link Expired</h2>
        <p>{{ errorMessage() }}</p>
        <a
          mat-flat-button
          color="primary"
          routerLink="/auth/forgot-password"
          class="full-width"
          aria-label="Request a new password reset link">
          Request New Link
        </a>
        <a
          routerLink="/auth/login"
          class="back-link">
          Back to Login
        </a>
      </div>
    } @else {
      <form
        [formGroup]="resetForm"
        (ngSubmit)="onSubmit()"
        class="auth-form">
        <p class="instructions">
          Enter your new password. It must meet the complexity requirements below.
        </p>

        @if (errorMessage(); as msg) {
          <div class="error-banner" role="alert">
            <mat-icon>error</mat-icon>
            <span>{{ msg }}</span>
          </div>
        }

        <!-- New Password Field -->
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>New Password</mat-label>
          <input
            matInput
            formControlName="newPassword"
            [type]="showPassword() ? 'text' : 'password'"
            autocomplete="new-password"
            [attr.aria-describedby]="'password-requirements'" />
          <button
            mat-icon-button
            matSuffix
            type="button"
            (click)="togglePasswordVisibility()"
            [attr.aria-label]="
              showPassword() ? 'Hide password' : 'Show password'
            ">
            <mat-icon>
              {{ showPassword() ? 'visibility_off' : 'visibility' }}
            </mat-icon>
          </button>
        </mat-form-field>

        <!-- Password Requirements Checklist -->
        <ul
          id="password-requirements"
          class="password-checklist"
          aria-label="Password requirements">
          <li [class.met]="resetForm.get('newPassword')?.value?.length >= 8">
            At least 8 characters
          </li>
          <li [class.met]="resetForm.get('newPassword')?.value | passwordCheck:'uppercase'">
            At least one uppercase letter
          </li>
          <li [class.met]="resetForm.get('newPassword')?.value | passwordCheck:'digit'">
            At least one number
          </li>
          <li [class.met]="resetForm.get('newPassword')?.value | passwordCheck:'special'">
            At least one special character
          </li>
        </ul>

        <!-- Confirm Password Field -->
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Confirm Password</mat-label>
          <input
            matInput
            formControlName="confirmPassword"
            [type]="showConfirmPassword() ? 'text' : 'password'"
            autocomplete="new-password"
            [attr.aria-describedby]="
              resetForm.get('confirmPassword')?.hasError('passwordMismatch')
                ? 'confirm-error'
                : null
            " />
          <button
            mat-icon-button
            matSuffix
            type="button"
            (click)="toggleConfirmPasswordVisibility()"
            [attr.aria-label]="
              showConfirmPassword()
                ? 'Hide confirm password'
                : 'Show confirm password'
            ">
            <mat-icon>
              {{ showConfirmPassword() ? 'visibility_off' : 'visibility' }}
            </mat-icon>
          </button>
          @if (resetForm.get('confirmPassword')?.hasError('passwordMismatch')
               && resetForm.get('confirmPassword')?.touched) {
            <mat-error id="confirm-error">
              Passwords do not match.
            </mat-error>
          }
        </mat-form-field>

        <button
          mat-flat-button
          color="primary"
          type="submit"
          class="full-width submit-btn"
          [disabled]="isLoading()"
          aria-label="Reset password">
          @if (isLoading()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            Reset Password
          }
        </button>

        <div class="auth-links">
          <a routerLink="/auth/login" class="back-link">
            Back to Login
          </a>
        </div>
      </form>
    }
  </div>
</div>
```

6. **Create a simple `PasswordCheckPipe`** for the password requirements checklist rendering:

```typescript
// client/src/app/shared/pipes/password-check.pipe.ts
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'passwordCheck', standalone: true })
export class PasswordCheckPipe implements PipeTransform {
  transform(value: string | null | undefined, rule: string): boolean {
    if (!value) return false;
    switch (rule) {
      case 'uppercase': return /[A-Z]/.test(value);
      case 'digit':     return /[0-9]/.test(value);
      case 'special':   return /[^a-zA-Z0-9]/.test(value);
      default:          return false;
    }
  }
}
```

7. **Create shared SCSS** for both password reset pages (matching SCR-003 layout — centered 480px single-column):

```scss
// forgot-password.component.scss & reset-password.component.scss
// Shared structure — both inherit from auth layout

.auth-container {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 24px;
  background: var(--mat-sys-surface-container-lowest, #f8f9fa);
}

.auth-card {
  background: var(--mat-sys-surface, #fff);
  border-radius: 12px;
  padding: 40px 32px;
  max-width: 480px;
  width: 100%;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
}

.auth-header {
  text-align: center;
  margin-bottom: 24px;

  h1 {
    font-size: 1.5rem;
    font-weight: 600;
    margin: 0 0 16px;
    color: var(--mat-sys-on-surface, #1a1a1a);
  }
}

.step-indicator {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  margin-bottom: 8px;

  .step {
    display: flex;
    align-items: center;
    gap: 6px;
    opacity: 0.5;

    &.active, &.completed {
      opacity: 1;
    }
  }

  .step-number {
    width: 28px;
    height: 28px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.875rem;
    font-weight: 600;
    background: var(--mat-sys-surface-variant, #e0e0e0);
    color: var(--mat-sys-on-surface-variant, #555);

    .active &, .completed & {
      background: var(--mat-sys-primary, #1976d2);
      color: var(--mat-sys-on-primary, #fff);
    }
  }

  .step-label {
    font-size: 0.875rem;
    color: var(--mat-sys-on-surface-variant, #555);
  }

  .step-connector {
    width: 40px;
    height: 2px;
    background: var(--mat-sys-outline-variant, #ccc);

    &.active {
      background: var(--mat-sys-primary, #1976d2);
    }
  }
}

.instructions {
  text-align: center;
  color: var(--mat-sys-on-surface-variant, #555);
  margin: 0 0 24px;
  line-height: 1.5;
}

.full-width {
  width: 100%;
}

.submit-btn {
  margin-top: 8px;
  height: 44px;
}

.auth-links {
  text-align: center;
  margin-top: 16px;
}

.back-link {
  color: var(--mat-sys-primary, #1976d2);
  text-decoration: none;
  font-size: 0.875rem;

  &:hover {
    text-decoration: underline;
  }
}

.confirmation {
  text-align: center;

  .success-icon {
    font-size: 48px;
    width: 48px;
    height: 48px;
    color: var(--mat-sys-primary, #1976d2);
    margin-bottom: 16px;
  }

  h2 {
    margin: 0 0 8px;
    font-size: 1.25rem;
    font-weight: 600;
  }

  p {
    color: var(--mat-sys-on-surface-variant, #555);
    line-height: 1.5;
    margin: 0 0 8px;
  }

  .note {
    font-size: 0.875rem;
    margin-bottom: 24px;
  }
}

.token-expired {
  text-align: center;

  .error-icon {
    font-size: 48px;
    width: 48px;
    height: 48px;
    color: var(--mat-sys-error, #dc2626);
    margin-bottom: 16px;
  }

  h2 {
    margin: 0 0 8px;
    font-size: 1.25rem;
    font-weight: 600;
  }

  p {
    color: var(--mat-sys-on-surface-variant, #555);
    margin: 0 0 24px;
    line-height: 1.5;
  }

  .back-link {
    display: block;
    margin-top: 16px;
  }
}

.error-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  border-radius: 8px;
  background: var(--mat-sys-error-container, #fde8e8);
  color: var(--mat-sys-on-error-container, #c62828);
  margin-bottom: 16px;
  font-size: 0.875rem;

  mat-icon {
    font-size: 20px;
    width: 20px;
    height: 20px;
    flex-shrink: 0;
  }
}

.password-checklist {
  list-style: none;
  padding: 0;
  margin: -8px 0 16px 4px;
  font-size: 0.8125rem;
  color: var(--mat-sys-on-surface-variant, #777);

  li {
    padding: 2px 0 2px 24px;
    position: relative;

    &::before {
      content: '✗';
      position: absolute;
      left: 4px;
      color: var(--mat-sys-error, #dc2626);
    }

    &.met {
      color: var(--mat-sys-on-surface, #333);

      &::before {
        content: '✓';
        color: var(--mat-sys-tertiary, #16a34a);
      }
    }
  }
}
```

8. **Modify `AuthService`** to add forgot-password and reset-password API methods:

```typescript
// Add to client/src/app/features/auth/services/auth.service.ts
import { Observable } from 'rxjs';

forgotPassword(email: string): Observable<{ message: string }> {
  return this.http.post<{ message: string }>(
    '/api/v1/auth/forgot-password',
    { email }
  );
}

resetPassword(
  email: string,
  token: string,
  newPassword: string
): Observable<{ message: string }> {
  return this.http.post<{ message: string }>(
    '/api/v1/auth/reset-password',
    { email, token, newPassword }
  );
}
```

9. **Update auth routing** to add forgot-password and reset-password routes:

```typescript
// Add to client/src/app/features/auth/auth-routing.module.ts
{
  path: 'forgot-password',
  loadComponent: () =>
    import('./pages/forgot-password/forgot-password.component')
      .then(m => m.ForgotPasswordComponent),
  title: 'Forgot Password',
},
{
  path: 'reset-password',
  loadComponent: () =>
    import('./pages/reset-password/reset-password.component')
      .then(m => m.ResetPasswordComponent),
  title: 'Reset Password',
},
```

10. **Enhance login page** to handle lockout banner and password-reset success banner:

```typescript
// In login.component.ts — read additional query param reasons
const reason = this.route.snapshot.queryParamMap.get('reason');
switch (reason) {
  case 'session-expired':
    this.bannerMessage = 'Session expired.';
    break;
  case 'session-ended':
    this.bannerMessage = 'Session ended.';
    break;
  case 'password-reset':
    this.bannerMessage = 'Password reset successfully. Please log in with your new password.';
    this.bannerType = 'success';
    break;
}
```

```html
<!-- In login.component.html — lockout error (AC-3) -->
<!-- Existing error handling enhanced for lockout-specific message -->
@if (loginError() === 'locked') {
  <div class="error-banner lockout" role="alert">
    <mat-icon>lock</mat-icon>
    <span>Account locked. Please try again in 30 minutes.</span>
  </div>
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.component.ts
            ├── app.config.ts
            ├── core/
            │   ├── interceptors/
            │   │   └── auth.interceptor.ts          (from US_014 task_002)
            │   ├── guards/
            │   │   └── auth.guard.ts                (from US_014 task_002)
            │   └── services/
            │       ├── token-storage.service.ts      (from US_014 task_002)
            │       ├── inactivity-timer.service.ts   (from US_017 task_002)
            │       └── session-signalr.service.ts    (from US_017 task_002)
            ├── features/
            │   └── auth/
            │       ├── services/
            │       │   └── auth.service.ts           (from US_014 task_002)
            │       ├── auth-routing.module.ts
            │       └── pages/
            │           └── login/
            │               ├── login.component.ts    (from US_014 task_002)
            │               ├── login.component.html
            │               └── login.component.scss
            └── shared/
                ├── components/
                │   └── session-timeout-modal/        (from US_017 task_002)
                ├── validators/
                └── pipes/
```

> Placeholder: Update on execution based on US_014 task_002 and US_017 task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/auth/pages/forgot-password/forgot-password.component.ts | Email input form, send reset link, generic confirmation display |
| CREATE | client/src/app/features/auth/pages/forgot-password/forgot-password.component.html | Step 1 template with email field, loading state, confirmation view |
| CREATE | client/src/app/features/auth/pages/forgot-password/forgot-password.component.scss | SCR-003 centered 480px layout, step indicator, confirmation styles |
| CREATE | client/src/app/features/auth/pages/reset-password/reset-password.component.ts | New password form with complexity validation, token handling, redirect |
| CREATE | client/src/app/features/auth/pages/reset-password/reset-password.component.html | Step 2 template with password fields, complexity checklist, expired-token view |
| CREATE | client/src/app/features/auth/pages/reset-password/reset-password.component.scss | Password form styles, checklist, error states |
| CREATE | client/src/app/shared/validators/password-strength.validator.ts | Reusable reactive form validators for password complexity and match |
| CREATE | client/src/app/shared/pipes/password-check.pipe.ts | Pure pipe for password rule checking in templates |
| MODIFY | client/src/app/features/auth/services/auth.service.ts | Add forgotPassword() and resetPassword() API methods |
| MODIFY | client/src/app/features/auth/auth-routing.module.ts | Add forgot-password and reset-password lazy routes |
| MODIFY | client/src/app/features/auth/pages/login/login.component.ts | Add lockout banner, password-reset success banner via query params |
| MODIFY | client/src/app/features/auth/pages/login/login.component.html | Add lockout-specific error banner with lock icon |

## External References

- Angular Reactive Forms Validators: https://angular.dev/guide/forms/reactive-forms#validating-form-input
- Angular Material Form Field: https://material.angular.io/components/form-field/overview
- WCAG 2.1 Success Criterion 1.3.5 — Identify Input Purpose: https://www.w3.org/WAI/WCAG21/Understanding/identify-input-purpose
- WCAG 2.1 Success Criterion 3.3.1 — Error Identification: https://www.w3.org/WAI/WCAG21/Understanding/error-identification
- OWASP Authentication Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html
- OWASP Forgot Password Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve frontend
ng serve

# Run tests
ng test

# Navigate to forgot password
# http://localhost:4200/auth/forgot-password

# Navigate to reset password (simulated link from email)
# http://localhost:4200/auth/reset-password?email=test@example.com&token=ENCODED_TOKEN
```

## Implementation Validation Strategy

- [ ] "Forgot Password" link on login page navigates to `/auth/forgot-password` (AC-1)
- [ ] Forgot-password form validates email format inline with `aria-describedby` error association (UXR-205)
- [ ] Submit shows loading spinner and then generic confirmation regardless of email existence (AC-1, UXR-501)
- [ ] Step indicator shows step 1 active on forgot-password, step 2 active on reset-password (SCR-003)
- [ ] Reset-password page reads `email` and `token` query parameters from URL (AC-2)
- [ ] Password complexity checklist updates in real-time as user types (AC-2)
- [ ] Password show/hide toggle works for both fields with accessible button labels
- [ ] Confirm-password field shows mismatch error when passwords differ (UXR-205)
- [ ] Successful reset redirects to `/auth/login?reason=password-reset` with success banner (AC-2)
- [ ] Expired/invalid token shows "Reset Link Expired" view with "Request New Link" button (edge case)
- [ ] Login page shows "Account locked. Please try again in 30 minutes." on lockout response (AC-3)
- [ ] Login page shows "Password reset successfully." banner when `reason=password-reset` (AC-2)
- [ ] All forms support full keyboard navigation with visible focus indicators (UXR-202)
- [ ] Color contrast meets WCAG 2.1 AA — 4.5:1 for normal text, 3:1 for large text (UXR-201)
- [ ] Layout is responsive across 375px, 768px, and 1440px breakpoints (UXR-301)

## Implementation Checklist

- [ ] Create `PasswordStrengthValidator` with rules: 8+ chars, uppercase, digit, special character
- [ ] Create `passwordMatchValidator` cross-field validator for confirm-password
- [ ] Create `PasswordCheckPipe` for template-driven checklist rendering
- [ ] Create `ForgotPasswordComponent` standalone component with email form and generic confirmation
- [ ] Create forgot-password HTML template with step indicator, email field, loading state, confirmation view
- [ ] Create forgot-password SCSS with centered 480px auth layout
- [ ] Create `ResetPasswordComponent` standalone component with password fields, complexity validation, token handling
- [ ] Create reset-password HTML template with step indicator, password fields, complexity checklist, expired-token view
- [ ] Create reset-password SCSS with checklist styles, error-banner, token-expired state
- [ ] Add `forgotPassword()` and `resetPassword()` methods to `AuthService`
- [ ] Add lazy routes for `/auth/forgot-password` and `/auth/reset-password` in auth routing
- [ ] Add lockout-specific error banner to login page template
- [ ] Add `password-reset` query param handling to login page for success banner
- [ ] Verify `aria-describedby` associations on all form error messages (UXR-205)
- [ ] Verify keyboard navigation through all form fields and buttons (UXR-202)
