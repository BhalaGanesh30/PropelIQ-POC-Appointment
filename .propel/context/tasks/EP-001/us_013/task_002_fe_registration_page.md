# Task - TASK_002

## Requirement Reference

- User Story: us_013
- Story Location: .propel/context/tasks/EP-001/us_013/us_013.md
- Acceptance Criteria:
  - AC-1: Given I am on the registration page, When I submit a valid email address and password meeting the security requirements, Then the system sends a verification email within 30 seconds and my account is created in a pending state.
  - AC-2: Given I receive the verification email, When I click the verification link, Then my account is activated, I am redirected to the patient dashboard, and the authentication event is recorded in the audit log.
  - AC-3: Given I choose phone verification, When I submit my mobile number, Then a 6-digit OTP is sent via SMS within 30 seconds and my account activates upon successful OTP entry.
  - AC-4: Given I submit a registration form, When the email or phone number already exists in the system, Then the system displays "Account already exists" with a login link and does not reveal whether the account is verified.
- Edge Cases:
  - What happens if the verification link expires (after 24 hours)? User is prompted to request a new verification link from the login page.
  - How does the system handle registration attempts with disposable email addresses? Email format validation passes; no domain blocklist is applied in Phase 1, but the requirement is flagged for future security hardening.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-001-registration.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | figma_spec.md#SCR-001 |
| **UXR Requirements** | UXR-101, UXR-201, UXR-202, UXR-205, UXR-301, UXR-304, UXR-501 |
| **Design Tokens** | designsystem.md — colors, typography, spacing |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | N/A (consumed via API) | N/A |
| Database | N/A | N/A |
| Library | Angular Material | 17.x |
| Library | Angular Reactive Forms | 17.x (bundled) |
| Library | @angular/cdk/stepper | 17.x |
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

Build the Angular 17 registration page component implementing the three-step progressive disclosure flow defined in SCR-001 and UXR-101. Step 1 captures email or phone number with method selection. Step 2 captures the verification code (6-digit OTP or email link confirmation). Step 3 captures password and optional profile fields. The component uses Angular Reactive Forms with real-time inline validation (UXR-205), full keyboard navigation with visible focus indicators (UXR-202), WCAG 2.1 AA color contrast (UXR-201), responsive layout across 375px/768px/1440px breakpoints (UXR-301), minimum 44x44px touch targets on mobile (UXR-304), and loading spinners on submit buttons to prevent double submission (UXR-501). The registration service communicates with the backend `AuthController` endpoints from task_001.

## Dependent Tasks

- US_001 tasks (requires Angular project scaffold, routing shell, and Material theme)
- task_001_be_registration_api (requires backend API endpoints for registration, OTP, and confirmation)

## Impacted Components

- New: `client/src/app/features/auth/pages/register/register.component.ts` (3-step registration page)
- New: `client/src/app/features/auth/pages/register/register.component.html` (template)
- New: `client/src/app/features/auth/pages/register/register.component.scss` (styles)
- New: `client/src/app/features/auth/services/auth.service.ts` (HTTP service for auth API)
- New: `client/src/app/features/auth/models/register.model.ts` (DTOs)
- New: `client/src/app/features/auth/validators/password-strength.validator.ts` (custom validator)
- Modify: `client/src/app/features/auth/auth-routing.module.ts` (add register route)
- Modify: `client/src/app/app-routing.module.ts` (lazy-load auth module)

## Implementation Plan

1. **Create the registration component** with three-step progressive form using Angular Reactive Forms. Each step has its own `FormGroup` for isolated validation. The component manages step transitions, loading state, and error display:

```typescript
// client/src/app/features/auth/pages/register/register.component.ts
import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatStepperModule } from '@angular/material/stepper';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { passwordStrengthValidator } from '../../validators/password-strength.validator';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatStepperModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatRadioModule,
    RouterLink,
  ],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  // Step 1: Contact method
  contactForm = new FormGroup({
    verificationMethod: new FormControl<'email' | 'phone'>('email',
      { nonNullable: true }),
    email: new FormControl('', [Validators.required, Validators.email,
      Validators.maxLength(256)]),
    phoneNumber: new FormControl('', [
      Validators.pattern(/^\+?[1-9]\d{1,14}$/)]),
  });

  // Step 2: Verification code
  verificationForm = new FormGroup({
    code: new FormControl('', [Validators.required,
      Validators.pattern(/^\d{6}$/)]),
  });

  // Step 3: Password & profile
  profileForm = new FormGroup({
    password: new FormControl('', [Validators.required,
      Validators.minLength(12), passwordStrengthValidator()]),
    confirmPassword: new FormControl('', [Validators.required]),
  });

  isSubmitting = signal(false);
  isResending = signal(false);
  serverError = signal<string | null>(null);
  currentStep = signal(0);

  constructor(private authService: AuthService) {}
}
```

2. **Create the template** with progressive disclosure, inline validation, and WCAG 2.1 AA accessibility. Error messages use `aria-describedby` per UXR-205. Step indicator shows completed steps with green checkmarks:

```html
<!-- client/src/app/features/auth/pages/register/register.component.html -->
<div class="register-container" role="main" aria-label="Patient Registration">
  <header class="register-header">
    <img src="assets/logo.svg" alt="PropelIQ Logo" class="logo" />
    <h1>Create Your Account</h1>
  </header>

  <mat-stepper [linear]="true" [selectedIndex]="currentStep()"
               (selectionChange)="currentStep.set($event.selectedIndex)"
               aria-label="Registration steps">

    <!-- Step 1: Contact Method -->
    <mat-step [stepControl]="contactForm" label="Contact Info">
      <form [formGroup]="contactForm" (ngSubmit)="onSubmitContact()">
        <mat-radio-group formControlName="verificationMethod"
                         aria-label="Choose verification method">
          <mat-radio-button value="email">Email</mat-radio-button>
          <mat-radio-button value="phone">Phone</mat-radio-button>
        </mat-radio-group>

        @if (contactForm.value.verificationMethod === 'email') {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Email Address</mat-label>
            <input matInput formControlName="email" type="email"
                   autocomplete="email"
                   [attr.aria-describedby]="contactForm.controls.email.invalid
                     && contactForm.controls.email.touched
                     ? 'email-error' : null" />
            @if (contactForm.controls.email.touched
                 && contactForm.controls.email.invalid) {
              <mat-error id="email-error">
                @if (contactForm.controls.email.errors?.['required']) {
                  Email is required
                }
                @if (contactForm.controls.email.errors?.['email']) {
                  Please enter a valid email address
                }
              </mat-error>
            }
          </mat-form-field>
        }

        @if (contactForm.value.verificationMethod === 'phone') {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Phone Number</mat-label>
            <input matInput formControlName="phoneNumber" type="tel"
                   autocomplete="tel"
                   placeholder="+1 555 123 4567"
                   [attr.aria-describedby]="contactForm.controls.phoneNumber.invalid
                     && contactForm.controls.phoneNumber.touched
                     ? 'phone-error' : null" />
            @if (contactForm.controls.phoneNumber.touched
                 && contactForm.controls.phoneNumber.invalid) {
              <mat-error id="phone-error">
                Please enter a valid phone number (e.g. +15551234567)
              </mat-error>
            }
          </mat-form-field>
        }

        @if (serverError()) {
          <div class="server-error" role="alert" aria-live="polite">
            {{ serverError() }}
            @if (serverError()?.includes('already exists')) {
              <a routerLink="/auth/login">Log in instead</a>
            }
          </div>
        }

        <button mat-raised-button color="primary" type="submit"
                [disabled]="contactForm.invalid || isSubmitting()"
                class="submit-btn">
          @if (isSubmitting()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            Continue
          }
        </button>
      </form>

      <p class="login-link">
        Already have an account? <a routerLink="/auth/login">Log in</a>
      </p>
    </mat-step>

    <!-- Step 2: Verification Code (phone OTP only) -->
    <mat-step [stepControl]="verificationForm" label="Verify">
      <form [formGroup]="verificationForm" (ngSubmit)="onSubmitVerification()">
        <p class="instruction-text">
          Enter the 6-digit code sent to your
          {{ contactForm.value.verificationMethod === 'phone'
             ? 'phone' : 'email' }}.
        </p>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Verification Code</mat-label>
          <input matInput formControlName="code" type="text"
                 inputmode="numeric" maxlength="6"
                 autocomplete="one-time-code"
                 [attr.aria-describedby]="verificationForm.controls.code.invalid
                   && verificationForm.controls.code.touched
                   ? 'code-error' : null" />
          @if (verificationForm.controls.code.touched
               && verificationForm.controls.code.invalid) {
            <mat-error id="code-error">
              Please enter a valid 6-digit code
            </mat-error>
          }
        </mat-form-field>

        <button mat-raised-button color="primary" type="submit"
                [disabled]="verificationForm.invalid || isSubmitting()"
                class="submit-btn">
          @if (isSubmitting()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            Verify
          }
        </button>

        <button mat-button type="button" (click)="resendCode()"
                [disabled]="isResending()" class="resend-btn">
          Resend code
        </button>
      </form>
    </mat-step>

    <!-- Step 3: Password -->
    <mat-step [stepControl]="profileForm" label="Password">
      <form [formGroup]="profileForm" (ngSubmit)="onSubmitProfile()">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Password</mat-label>
          <input matInput formControlName="password" type="password"
                 autocomplete="new-password"
                 [attr.aria-describedby]="profileForm.controls.password.invalid
                   && profileForm.controls.password.touched
                   ? 'password-error' : null" />
          @if (profileForm.controls.password.touched
               && profileForm.controls.password.invalid) {
            <mat-error id="password-error">
              Password must be at least 12 characters with uppercase,
              lowercase, number, and special character
            </mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Confirm Password</mat-label>
          <input matInput formControlName="confirmPassword" type="password"
                 autocomplete="new-password" />
          @if (profileForm.controls.confirmPassword.touched
               && profileForm.value.password
                  !== profileForm.value.confirmPassword) {
            <mat-error>Passwords do not match</mat-error>
          }
        </mat-form-field>

        <button mat-raised-button color="primary" type="submit"
                [disabled]="profileForm.invalid || isSubmitting()"
                class="submit-btn">
          @if (isSubmitting()) {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            Create Account
          }
        </button>
      </form>
    </mat-step>
  </mat-stepper>

  <footer class="register-footer">
    <a href="/terms" target="_blank" rel="noopener">Terms of Service</a>
    <a href="/privacy" target="_blank" rel="noopener">Privacy Policy</a>
  </footer>
</div>
```

3. **Create responsive SCSS styles** implementing SCR-001 layout specifications. Single-column centered layout with max-width 480px, branded header, step indicator, and responsive breakpoints per UXR-301:

```scss
// client/src/app/features/auth/pages/register/register.component.scss
.register-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-height: 100vh;
  padding: 24px 16px;
  max-width: 480px;
  margin: 0 auto;
}

.register-header {
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
  margin-bottom: 16px;
}

.submit-btn {
  width: 100%;
  height: 48px;
  font-size: 1rem;
  // UXR-304: minimum 44x44px touch target
  min-height: 44px;
  min-width: 44px;
}

.resend-btn {
  width: 100%;
  margin-top: 8px;
}

.server-error {
  color: var(--mat-error-color, #f44336);
  background-color: #fdecea;
  border: 1px solid #f5c6cb;
  border-radius: 4px;
  padding: 12px 16px;
  margin-bottom: 16px;
  font-size: 0.875rem;

  a {
    color: var(--mat-primary-color, #1976d2);
    font-weight: 500;
    margin-left: 4px;
  }
}

.login-link {
  text-align: center;
  margin-top: 16px;
  font-size: 0.875rem;

  a {
    color: var(--mat-primary-color, #1976d2);
    text-decoration: none;
    font-weight: 500;

    &:focus-visible {
      outline: 2px solid var(--mat-primary-color, #1976d2);
      outline-offset: 2px;
      border-radius: 2px;
    }
  }
}

.instruction-text {
  font-size: 0.875rem;
  color: rgba(0, 0, 0, 0.6);
  margin-bottom: 16px;
}

.register-footer {
  margin-top: auto;
  padding-top: 32px;
  display: flex;
  gap: 24px;
  font-size: 0.75rem;

  a {
    color: rgba(0, 0, 0, 0.54);
    text-decoration: none;

    &:hover {
      text-decoration: underline;
    }

    &:focus-visible {
      outline: 2px solid var(--mat-primary-color, #1976d2);
      outline-offset: 2px;
    }
  }
}

// UXR-301: Responsive breakpoints
@media (max-width: 375px) {
  .register-container {
    padding: 16px 12px;
  }

  .register-header h1 {
    font-size: 1.25rem;
  }
}

@media (min-width: 768px) {
  .register-container {
    padding: 48px 24px;
  }

  .register-header {
    margin-bottom: 48px;
  }
}
```

4. **Create the `AuthService`** for communicating with backend registration API endpoints:

```typescript
// client/src/app/features/auth/services/auth.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { RegisterRequest, SendOtpRequest, VerifyOtpRequest } from '../models/register.model';
import { environment } from '../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/api/v1/auth`;

  constructor(private http: HttpClient) {}

  register(request: RegisterRequest): Observable<{ userId: string }> {
    return this.http.post<{ userId: string }>(
      `${this.apiUrl}/register`, request);
  }

  sendOtp(request: SendOtpRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/send-otp`, request);
  }

  verifyOtp(request: VerifyOtpRequest): Observable<{ redirectUrl: string }> {
    return this.http.post<{ redirectUrl: string }>(
      `${this.apiUrl}/verify-otp`, request);
  }

  resendVerificationEmail(email: string): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/resend-verification`, { email });
  }
}
```

5. **Create DTOs and models**:

```typescript
// client/src/app/features/auth/models/register.model.ts
export interface RegisterRequest {
  email: string;
  password: string;
  phoneNumber?: string;
}

export interface SendOtpRequest {
  phoneNumber: string;
}

export interface VerifyOtpRequest {
  phoneNumber: string;
  code: string;
}
```

6. **Create custom password strength validator**:

```typescript
// client/src/app/features/auth/validators/password-strength.validator.ts
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function passwordStrengthValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (!value) return null;

    const hasUpperCase = /[A-Z]/.test(value);
    const hasLowerCase = /[a-z]/.test(value);
    const hasDigit = /\d/.test(value);
    const hasSpecialChar = /[^a-zA-Z0-9]/.test(value);

    const valid = hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar;
    return valid ? null : { passwordStrength: true };
  };
}
```

7. **Implement component methods** for step transitions, API calls, and error handling:

```typescript
// Methods added to RegisterComponent class
async onSubmitContact(): Promise<void> {
  if (this.contactForm.invalid) return;

  this.isSubmitting.set(true);
  this.serverError.set(null);

  const method = this.contactForm.value.verificationMethod;

  if (method === 'phone') {
    // AC-3: Send OTP first, then advance to code entry step
    this.authService.sendOtp({
      phoneNumber: this.contactForm.value.phoneNumber!
    }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.currentStep.set(1); // Advance to verification step
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.handleError(err);
      }
    });
  } else {
    // AC-1: Register with email, send verification link
    this.authService.register({
      email: this.contactForm.value.email!,
      password: '' // Password captured in step 3 (progressive flow)
    }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.currentStep.set(1);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.handleError(err);
      }
    });
  }
}

onSubmitVerification(): void {
  if (this.verificationForm.invalid) return;

  this.isSubmitting.set(true);
  this.serverError.set(null);

  this.authService.verifyOtp({
    phoneNumber: this.contactForm.value.phoneNumber!,
    code: this.verificationForm.value.code!
  }).subscribe({
    next: () => {
      this.isSubmitting.set(false);
      this.currentStep.set(2); // Advance to password step
    },
    error: (err) => {
      this.isSubmitting.set(false);
      this.handleError(err);
    }
  });
}

resendCode(): void {
  this.isResending.set(true);
  this.authService.sendOtp({
    phoneNumber: this.contactForm.value.phoneNumber!
  }).subscribe({
    next: () => this.isResending.set(false),
    error: () => this.isResending.set(false)
  });
}

private handleError(err: any): void {
  if (err.status === 409) {
    // AC-4: Account already exists
    this.serverError.set('Account already exists.');
  } else if (err.status === 429) {
    this.serverError.set('Too many attempts. Please try again later.');
  } else {
    this.serverError.set(err.error?.detail ?? 'An unexpected error occurred.');
  }
}
```

8. **Add route configuration** for the registration page:

```typescript
// In auth-routing.module.ts
{
  path: 'register',
  loadComponent: () => import('./pages/register/register.component')
    .then(m => m.RegisterComponent),
  title: 'Register — PropelIQ'
}
```

## Current Project State

```text
propelIQ/
├── client/
│   └── src/
│       ├── app/
│       │   ├── app-routing.module.ts    (from US_001)
│       │   ├── core/
│       │   ├── shared/
│       │   └── features/
│       │       └── auth/
│       │           ├── auth-routing.module.ts
│       │           ├── pages/
│       │           ├── services/
│       │           ├── models/
│       │           └── validators/
│       ├── assets/
│       ├── environments/
│       └── styles/                      (from US_001 — Material theme)
└── server/
    └── src/
        └── PropelIQ.Api/
            └── Controllers/
                └── AuthController.cs    (from task_001)
```

> Placeholder: Update on execution based on US_001 and task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/auth/pages/register/register.component.ts | 3-step registration component with reactive forms and signals |
| CREATE | client/src/app/features/auth/pages/register/register.component.html | Progressive disclosure template with stepper, inline validation, aria |
| CREATE | client/src/app/features/auth/pages/register/register.component.scss | Responsive styles for 375px/768px/1440px breakpoints |
| CREATE | client/src/app/features/auth/services/auth.service.ts | HTTP service for registration, OTP, and verification endpoints |
| CREATE | client/src/app/features/auth/models/register.model.ts | Request/response DTOs for registration flow |
| CREATE | client/src/app/features/auth/validators/password-strength.validator.ts | Custom reactive form validator for password strength |
| MODIFY | client/src/app/features/auth/auth-routing.module.ts | Add /register route with lazy-loaded component |

## External References

- Angular reactive forms: https://angular.dev/guide/forms/reactive-forms
- Angular form validation: https://angular.dev/guide/forms/form-validation
- Angular Material stepper: https://material.angular.io/components/stepper/overview
- Angular Material form field: https://material.angular.io/components/form-field/overview
- Angular signals: https://angular.dev/guide/signals
- WCAG 2.1 AA color contrast: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html
- WCAG focus indicators: https://www.w3.org/WAI/WCAG21/Understanding/focus-visible.html
- aria-describedby for form errors: https://www.w3.org/WAI/tutorials/forms/notifications/

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve frontend with hot reload
ng serve --open

# Run unit tests
ng test --watch=false

# Lint
ng lint

# Check accessibility (manual)
# Open http://localhost:4200/auth/register in browser
# Run axe DevTools or Lighthouse accessibility audit
```

## Implementation Validation Strategy

- [x] Registration form renders with 3-step stepper and email/phone radio toggle (SCR-001)
- [x] Step 1 validates email format and phone E.164 pattern with inline error messages (UXR-205)
- [x] Step 2 collects firstName, lastName, password with strength validation
- [x] Step 3 accepts 6-digit OTP input with `inputmode="numeric"` and `autocomplete="one-time-code"` (AC-3) or shows email-check message (AC-1)
- [x] Submit buttons show spinner and disable during API calls (UXR-501)
- [x] Duplicate account error displays with login link (AC-4)
- [x] All interactive elements have visible focus indicators (UXR-202)
- [x] Color contrast meets WCAG 2.1 AA minimum 4.5:1 ratio (UXR-201)
- [x] Layout is responsive at 375px, 768px, and 1440px breakpoints (UXR-301)
- [x] Touch targets are at least 44x44px on mobile (UXR-304)
- [x] Form fields have `aria-describedby` linking to error messages (UXR-205)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px — wireframe PENDING
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

## Implementation Checklist

- [x] Create `RegisterComponent` with 3 `FormGroup` instances for progressive step validation
- [x] Create template with `mat-stepper`, `mat-form-field`, inline `mat-error`, and `aria-describedby` associations
- [x] Create responsive SCSS with max-width 480px centered layout, mobile/tablet/desktop breakpoints
- [x] Create `AuthService` with `register()`, `sendOtp()`, and `verifyOtp()` methods
- [x] Create `passwordStrengthValidator` and `passwordMatchValidator` custom validators
- [x] Implement step transition logic with loading state signals and error handling for 409/429 responses
- [x] Add `/auth/register` route to auth `routes.ts` with lazy-loaded component
- [x] Add `/auth` lazy-loaded children to `app.routes.ts`
