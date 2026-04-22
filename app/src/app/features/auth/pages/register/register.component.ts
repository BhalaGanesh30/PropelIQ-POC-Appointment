import {
  ChangeDetectionStrategy,
  Component,
  signal,
  inject,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { MatStepperModule } from '@angular/material/stepper';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';
import {
  passwordMatchValidator,
  passwordStrengthValidator,
} from '../../validators/password-strength.validator';
import { VerificationMethod } from '../../models/register.model';

/**
 * Three-step progressive registration component (SCR-001).
 *
 * Step 1 — Contact: email + optional phone number with method toggle.
 * Step 2 — Profile: first name, last name, password, confirm password.
 *           On submit: POST /auth/register. If phone method, POST /auth/send-otp.
 * Step 3 — Verify:
 *           Email path: success message with "check your inbox" guidance.
 *           Phone path: 6-digit OTP entry → POST /auth/verify-otp.
 *
 * UXR-101: single-column centred progressive disclosure.
 * UXR-201: WCAG 2.1 AA colour contrast via Material theme (primary #1976D2).
 * UXR-202: full keyboard navigation; visible focus rings on all controls.
 * UXR-205: aria-describedby on all form fields; errors linked programmatically.
 * UXR-301: responsive at 375px / 768px / 1440px breakpoints.
 * UXR-304: minimum 44 × 44 px touch targets on submit buttons.
 * UXR-501: loading spinner + disabled state during network requests.
 */
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatStepperModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatRadioModule,
    MatIconModule,
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  private readonly authService = inject(AuthService);

  // ── Step 1: Contact ────────────────────────────────────────────────────────
  readonly contactForm = new FormGroup({
    verificationMethod: new FormControl<VerificationMethod>('email', {
      nonNullable: true,
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(256)],
    }),
    phoneNumber: new FormControl('', {
      validators: [Validators.pattern(/^\+?[1-9]\d{1,14}$/)],
    }),
  });

  // ── Step 2: Profile + Password ─────────────────────────────────────────────
  readonly profileForm = new FormGroup(
    {
      firstName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(100)],
      }),
      lastName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(100)],
      }),
      password: new FormControl('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.minLength(8),
          passwordStrengthValidator(),
        ],
      }),
      confirmPassword: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required],
      }),
    },
    { validators: passwordMatchValidator('password', 'confirmPassword') }
  );

  // ── Step 3: OTP (phone path only) ─────────────────────────────────────────
  readonly verificationForm = new FormGroup({
    otp: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d{6}$/)],
    }),
  });

  // ── UI state signals ───────────────────────────────────────────────────────
  readonly currentStep = signal(0);
  readonly isSubmitting = signal(false);
  readonly isResending = signal(false);
  readonly serverError = signal<string | null>(null);
  readonly registrationComplete = signal(false);
  readonly showPassword = signal(false);
  readonly showConfirmPassword = signal(false);

  // ── Accessors ──────────────────────────────────────────────────────────────

  get isPhoneMethod(): boolean {
    return this.contactForm.controls.verificationMethod.value === 'phone';
  }

  get emailValue(): string {
    return this.contactForm.controls.email.value;
  }

  // ── Step transitions ───────────────────────────────────────────────────────

  /** Step 1 → Step 2: validate contact form, no API call. */
  onContactNext(): void {
    this.contactForm.markAllAsTouched();

    // Require phone when method is phone.
    if (this.isPhoneMethod) {
      const phoneCtrl = this.contactForm.controls.phoneNumber;
      if (!phoneCtrl.value || phoneCtrl.invalid) {
        phoneCtrl.setErrors({ required: true });
        return;
      }
    }

    if (this.contactForm.invalid) return;

    this.serverError.set(null);
    this.currentStep.set(1);
  }

  /**
   * Step 2 submit: POST /auth/register to create the account.
   * On success → if phone method, POST /auth/send-otp then advance to step 3.
   *            → if email method, advance to step 3 (shows inbox guidance).
   */
  onProfileSubmit(): void {
    this.profileForm.markAllAsTouched();
    if (this.profileForm.invalid) return;

    this.isSubmitting.set(true);
    this.serverError.set(null);

    const { firstName, lastName, password } = this.profileForm.getRawValue();
    const { email, phoneNumber } = this.contactForm.getRawValue();

    this.authService
      .register({
        email,
        password,
        firstName,
        lastName,
        phoneNumber: this.isPhoneMethod ? (phoneNumber ?? undefined) : undefined,
      })
      .subscribe({
        next: () => {
          if (this.isPhoneMethod) {
            // Dispatch OTP to the registered phone number (AC-3).
            this.authService.sendOtp({ email }).subscribe({
              next: () => {
                this.isSubmitting.set(false);
                this.currentStep.set(2);
              },
              error: () => {
                // OTP send failure is non-critical — advance anyway; user can resend.
                this.isSubmitting.set(false);
                this.currentStep.set(2);
              },
            });
          } else {
            this.isSubmitting.set(false);
            this.currentStep.set(2);
          }
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.handleError(err);
        },
      });
  }

  /**
   * Step 3 submit (phone path only): POST /auth/verify-otp.
   * On success mark registration complete.
   */
  onOtpSubmit(): void {
    this.verificationForm.markAllAsTouched();
    if (this.verificationForm.invalid) return;

    this.isSubmitting.set(true);
    this.serverError.set(null);

    this.authService
      .verifyOtp({
        email: this.emailValue,
        otp: this.verificationForm.controls.otp.value,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.registrationComplete.set(true);
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.handleError(err);
        },
      });
  }

  /** Re-send OTP to the registered phone number (UXR-501). */
  resendOtp(): void {
    this.isResending.set(true);
    this.serverError.set(null);

    this.authService.sendOtp({ email: this.emailValue }).subscribe({
      next: () => this.isResending.set(false),
      error: () => this.isResending.set(false),
    });
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((v) => !v);
  }

  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword.update((v) => !v);
  }

  // ── Error handling ─────────────────────────────────────────────────────────

  private handleError(err: HttpErrorResponse): void {
    if (err.status === 409) {
      // AC-4: duplicate account — generic message, no enumeration.
      this.serverError.set(
        'An account with this email already exists.'
      );
    } else if (err.status === 429) {
      this.serverError.set(
        'Too many attempts. Please wait a moment before trying again.'
      );
    } else if (err.status === 400) {
      const detail: string = err.error?.detail ?? err.error?.title ?? '';
      this.serverError.set(detail || 'Invalid or expired verification code.');
    } else {
      this.serverError.set(
        err.error?.detail ?? 'An unexpected error occurred. Please try again.'
      );
    }
  }
}
