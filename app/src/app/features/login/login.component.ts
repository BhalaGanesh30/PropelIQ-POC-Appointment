import { ChangeDetectionStrategy, Component, OnInit, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../auth/services/auth.service';
import { TokenStorageService } from '../../core/services/token-storage.service';

@Component({
  selector: 'app-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnInit {
  loginForm = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
    password: new FormControl('', [Validators.required]),
    rememberMe: new FormControl(false),
  });

  isSubmitting = signal(false);
  serverError = signal<string | null>(null);
  showPassword = signal(false);
  /** Session-related or password-reset info banner. */
  sessionBanner = signal<string | null>(null);
  /** 'info' = session banners | 'success' = password-reset confirmation | 'lockout' = locked account (AC-3). */
  bannerType = signal<'info' | 'success' | 'lockout'>('info');

  constructor(
    private readonly authService: AuthService,
    private readonly tokenStorage: TokenStorageService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    const reason = this.route.snapshot.queryParamMap.get('reason');
    if (reason === 'session-expired') {
      this.bannerType.set('info');
      this.sessionBanner.set('Session expired. Please log in again.');
    } else if (reason === 'session-ended') {
      this.bannerType.set('info');
      this.sessionBanner.set('Your session was ended because you logged in from another device.');
    } else if (reason === 'password-reset') {
      // AC-2 (us_018): show success confirmation after a password reset.
      this.bannerType.set('success');
      this.sessionBanner.set('Password reset successfully. Please log in with your new password.');
    }
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((v) => !v);
  }

  onSubmit(): void {
    if (this.loginForm.invalid || this.isSubmitting()) return;

    this.isSubmitting.set(true);
    this.serverError.set(null);

    const { email, password, rememberMe } = this.loginForm.value;

    // AC-1: configure storage based on remember-me checkbox.
    if (rememberMe) {
      this.tokenStorage.useLocalStorage();
    } else {
      this.tokenStorage.useSessionStorage();
    }

    this.authService.login({ email: email!, password: password! }).subscribe({
      next: (response) => {
        this.tokenStorage.saveTokens(
          response.accessToken,
          response.refreshToken
        );
        // Persist the server-side session token for session/extend calls (us_017).
        if (response.sessionToken) {
          this.tokenStorage.saveSessionToken(response.sessionToken);
        }
        this.isSubmitting.set(false);
        // AC-1: redirect to role-appropriate dashboard returned by the server.
        this.router.navigateByUrl(response.redirectUrl ?? '/dashboard');
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const traceId: string | undefined = err.error?.traceId;
        const correlationId: string | null = err.headers?.get('X-Correlation-Id') ?? null;

        if (err.status === 401) {
          const title: string = err.error?.title ?? '';
          // AC-3 (us_018): locked account returns a distinct title — surface dedicated banner.
          if (title.toLowerCase().includes('locked')) {
            this.bannerType.set('lockout');
            this.sessionBanner.set('Account locked. Please try again in 30 minutes.');
            this.serverError.set(null);
          } else {
            // AC-3 (us_014): never distinguish wrong email vs. wrong password.
            this.serverError.set(title || 'Invalid username or password');
          }
        } else if (err.status === 429) {
          this.serverError.set(
            'Too many login attempts. Please try again later.'
          );
        } else if (err.status === 500) {
          const ref = traceId || correlationId;
          this.serverError.set(
            ref
              ? `Server error during login. Please contact support with reference: ${ref}`
              : 'Server error during login. Please try again later.'
          );
        } else {
          this.serverError.set('An unexpected error occurred. Please try again.');
        }
      },
    });
  }
}
