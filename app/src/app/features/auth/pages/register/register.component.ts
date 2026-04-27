import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
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
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
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
 * Matches the Hi-Fi wireframe auth-card two-column layout.
 *
 * Step 1 "Contact"  — email + optional phone + method toggle.
 * Step 2 "Verify"   — Phase A: full name + create password → POST /auth/register.
 *                     Phone path: POST /auth/send-otp → Phase B (OTP entry).
 *                     Email path: inbox guidance shown.
 *                     Phase B: 6-digit OTP → POST /auth/verify-otp → step 3.
 * Step 3 "Profile"  — Success / registration complete.
 *
 * UXR-101: two-column auth-card; progressive disclosure per step.
 * UXR-201: WCAG 2.1 AA colour contrast via design-system tokens.
 * UXR-202: full keyboard navigation; 2 px focus rings on all controls.
 * UXR-205: aria-describedby on all inputs; errors linked programmatically.
 * UXR-301: responsive at 375 px / 768 px / 1440 px.
 * UXR-304: minimum 44 × 44 px touch targets on action buttons.
 * UXR-501: loading spinner + disabled state during network requests.
 */
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
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

  // ── Step 2 Phase A: Profile + Password ────────────────────────────────────
  readonly profileForm = new FormGroup(
    {
      fullName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(200)],
      }),
      password: new FormControl('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.minLength(12),
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

  // ── Step 2 Phase B: OTP (phone path only) ─────────────────────────────────
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
  /** True after POST /auth/register succeeds; reveals OTP or inbox panel on step 2. */
  readonly accountCreated = signal(false);
  readonly showPassword = signal(false);
  readonly showConfirmPassword = signal(false);

  // ── OTP countdown signals (F-4) ────────────────────────────────────────────
  readonly otpSecondsLeft = signal(271); // 4 min 31 s (wireframe SCR-001)
  readonly resendCooldownLeft = signal(0);

  readonly otpCountdownDisplay = computed(() => {
    const s = this.otpSecondsLeft();
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return `${m.toString().padStart(2, '0')}:${sec.toString().padStart(2, '0')}`;
  });

  readonly canResend = computed(
    () => this.resendCooldownLeft() === 0 && !this.isResending()
  );

  private otpTimerInterval: ReturnType<typeof setInterval> | null = null;
  private resendTimerInterval: ReturnType<typeof setInterval> | null = null;
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.destroyRef.onDestroy(() => {
      if (this.otpTimerInterval) clearInterval(this.otpTimerInterval);
      if (this.resendTimerInterval) clearInterval(this.resendTimerInterval);
    });
  }

  // ── Derived values ─────────────────────────────────────────────────────────

  get isPhoneMethod(): boolean {
    return this.contactForm.controls.verificationMethod.value === 'phone';
  }

  get emailValue(): string {
    return this.contactForm.controls.email.value;
  }

  /** Live password-strength label driven by current input value (used for chips). */
  get passwordStrength(): 'weak' | 'medium' | 'strong' | null {
    const pw = this.profileForm.controls.password.value;
    if (!pw || pw.length < 4) return null;
    let score = 0;
    if (pw.length >= 12) score++;
    if (/[A-Z]/.test(pw)) score++;
    if (/[0-9]/.test(pw)) score++;
    if (/[^A-Za-z0-9]/.test(pw)) score++;
    if (pw.length >= 16) score++;
    if (score <= 2) return 'weak';
    if (score === 3) return 'medium';
    return 'strong';
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

  /** Back button: return to the previous step (only available before account creation). */
  goBack(): void {
    this.serverError.set(null);
    if (this.currentStep() > 0) {
      this.currentStep.update((v) => v - 1);
    }
  }

  /**
   * Step 2 Phase A: POST /auth/register.
   * Phone path → POST /auth/send-otp → accountCreated(true) → Phase B (OTP).
   * Email path → accountCreated(true) → inbox panel shown.
   */
  onProfileSubmit(): void {
    this.profileForm.markAllAsTouched();
    if (this.profileForm.invalid) return;

    this.isSubmitting.set(true);
    this.serverError.set(null);

    const { fullName, password } = this.profileForm.getRawValue();
    const { email, phoneNumber } = this.contactForm.getRawValue();

    // Split "First Last" → firstName / lastName; single-word name fallback.
    const parts = fullName.trim().split(/\s+/);
    const firstName = parts[0] ?? fullName.trim();
    const lastName = parts.length > 1 ? parts.slice(1).join(' ') : parts[0];

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
            this.authService.sendOtp({ email }).subscribe({
              next: () => {
                this.isSubmitting.set(false);
                this.startOtpCountdown();
                this.accountCreated.set(true);
              },
              error: () => {
                // OTP send failure is non-critical — Phase B still shown; user can resend.
                this.isSubmitting.set(false);
                this.startOtpCountdown();
                this.accountCreated.set(true);
              },
            });
          } else {
            this.isSubmitting.set(false);
            this.accountCreated.set(true);
          }
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.handleError(err);
        },
      });
  }

  /**
   * Step 2 Phase B (phone path only): POST /auth/verify-otp.
   * On success advance to step 3 (Profile / complete).
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
          this.currentStep.set(2);
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.handleError(err);
        },
      });
  }

  /** Re-send OTP to the registered phone number (UXR-501). */
  resendOtp(): void {
    if (!this.canResend()) return;
    this.isResending.set(true);
    this.serverError.set(null);

    this.authService.sendOtp({ email: this.emailValue }).subscribe({
      next: () => {
        this.isResending.set(false);
        this.startOtpCountdown();
      },
      error: () => this.isResending.set(false),
    });
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((v) => !v);
  }

  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword.update((v) => !v);
  }

  // ── OTP timer helpers ──────────────────────────────────────────────────────

  private startOtpCountdown(): void {
    if (this.otpTimerInterval) clearInterval(this.otpTimerInterval);
    this.otpSecondsLeft.set(271);
    this.otpTimerInterval = setInterval(() => {
      const s = this.otpSecondsLeft();
      if (s > 0) {
        this.otpSecondsLeft.set(s - 1);
      } else {
        clearInterval(this.otpTimerInterval!);
        this.otpTimerInterval = null;
      }
    }, 1000);
    this.startResendCooldown();
  }

  private startResendCooldown(): void {
    if (this.resendTimerInterval) clearInterval(this.resendTimerInterval);
    this.resendCooldownLeft.set(30);
    this.resendTimerInterval = setInterval(() => {
      const c = this.resendCooldownLeft();
      if (c > 0) {
        this.resendCooldownLeft.set(c - 1);
      } else {
        clearInterval(this.resendTimerInterval!);
        this.resendTimerInterval = null;
      }
    }, 1000);
  }

  // ── Error handling ─────────────────────────────────────────────────────────

  private handleError(err: HttpErrorResponse): void {
    if (err.status === 409) {
      this.serverError.set('An account with this email already exists.');
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
