import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';
import { PasswordCheckPipe } from '../../../../shared/pipes/password-check.pipe';
import {
  passwordMatchValidator,
  passwordStrengthValidator,
} from '../../../../shared/validators/password-strength.validator';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    PasswordCheckPipe,
  ],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss',
})
export class ResetPasswordComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  readonly isLoading = signal(false);
  readonly showPassword = signal(false);
  readonly showConfirmPassword = signal(false);
  readonly errorMessage = signal<string | null>(null);
  /** True when the token is missing, invalid, or expired (edge case: 24-hour expiry). */
  readonly isTokenExpired = signal(false);

  private email = '';
  private token = '';

  readonly resetForm = new FormGroup(
    {
      newPassword: new FormControl('', [
        Validators.required,
        passwordStrengthValidator(),
      ]),
      confirmPassword: new FormControl('', [Validators.required]),
    },
    { validators: passwordMatchValidator('newPassword', 'confirmPassword') },
  );

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';

    if (!this.email || !this.token) {
      this.errorMessage.set('This reset link is missing required parameters.');
      this.isTokenExpired.set(true);
    }
  }

  /** Per-rule errors from the passwordStrength composite validator. */
  get passwordStrengthErrors(): Record<string, string> | null {
    const ctrl = this.resetForm.get('newPassword');
    if (!ctrl?.touched || !ctrl.errors?.['passwordStrength']) return null;
    return ctrl.errors['passwordStrength'] as Record<string, string>;
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((v) => !v);
  }

  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword.update((v) => !v);
  }

  onSubmit(): void {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService
      .resetPassword(this.email, this.token, this.resetForm.value.newPassword!)
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          // AC-2: redirect to login with success banner.
          void this.router.navigate(['/login'], {
            queryParams: { reason: 'password-reset' },
          });
        },
        error: (err: { status: number }) => {
          this.isLoading.set(false);
          if (err.status === 400) {
            // Edge case: 24-hour expiry or already-used token.
            this.errorMessage.set('The reset link is invalid or has expired.');
            this.isTokenExpired.set(true);
          } else {
            this.errorMessage.set('An unexpected error occurred. Please try again.');
          }
        },
      });
  }
}
