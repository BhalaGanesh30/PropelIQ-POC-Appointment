import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import {
  LoginRequest,
  LoginResponse,
  LogoutRequest,
  RefreshRequest,
  RegisterRequest,
  RegisterResponse,
  SendOtpRequest,
  VerifyOtpRequest,
} from '../models/register.model';
import { environment } from '../../../../environments/environment';
import { TokenStorageService } from '../../../core/services/token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  /** Base URL matches backend AuthController route: /api/v1/auth */
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  private readonly http = inject(HttpClient);
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly router = inject(Router);

  /**
   * Create a new user account in pending state (AC-1).
   * Backend dispatches email confirmation link automatically.
   */
  register(request: RegisterRequest): Observable<RegisterResponse> {
    return this.http.post<RegisterResponse>(`${this.baseUrl}/register`, request);
  }

  /**
   * Dispatch a 6-digit OTP to the user's registered phone number (AC-3).
   * Must be called after account creation — backend looks up user by email.
   */
  sendOtp(request: SendOtpRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/send-otp`, request);
  }

  /**
   * Validate the submitted OTP code against the Redis-stored value (AC-3).
   * Single-use: OTP is consumed on successful verification.
   */
  verifyOtp(request: VerifyOtpRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/verify-otp`,
      request
    );
  }

  /**
   * Validate credentials and receive a JWT access token + refresh token pair (AC-1).
   * The response includes a role-appropriate `redirectUrl` for dashboard routing.
   */
  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request);
  }

  /**
   * Exchange an expired access token + valid refresh token for a new rotated pair (AC-2).
   * Called automatically by `authInterceptor` on 401 responses.
   */
  refresh(request: RefreshRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/refresh`, request);
  }

  /**
   * Revoke the current refresh token server-side and clear local token storage (AC-4).
   * Caller is responsible for navigating to /login after the returned Observable completes.
   */
  logout(request: LogoutRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, request);
  }

  /**
   * Request a password reset email (us_018 AC-1).
   * Backend always returns 200 regardless of whether the email is registered.
   */
  forgotPassword(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/forgot-password`, { email });
  }

  /**
   * Submit a new password using the token from the reset email (us_018 AC-2).
   * Returns 400 if the token is invalid or expired (24-hour TTL).
   */
  resetPassword(
    email: string,
    token: string,
    newPassword: string,
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/reset-password`, {
      email,
      token,
      newPassword,
    });
  }

  /**
   * Reset the server-side inactivity timer for the current session (us_017 AC-4).
   * On failure the session is considered expired and the user is logged out.
   */
  extendSession(): void {
    const sessionToken = this.tokenStorage.getSessionToken();
    if (!sessionToken) return;

    this.http
      .post<{ expiresInSeconds: number }>(`${this.baseUrl}/session/extend`, {
        sessionToken,
      })
      .subscribe({
        error: () => {
          // Server rejected extend (expired server-side) — force logout.
          this.forceLogout('session-expired');
        },
      });
  }

  /**
   * Clear local tokens and redirect to login with the given reason query param.
   * Used by session timeout / SignalR displacement flows (us_017).
   */
  forceLogout(reason: 'session-expired' | 'session-ended'): void {
    const refreshToken = this.tokenStorage.getRefreshToken();
    this.tokenStorage.clearTokens();

    // Best-effort server-side revocation — do not await.
    if (refreshToken) {
      this.http
        .post<void>(`${this.baseUrl}/logout`, { refreshToken })
        .subscribe({ error: () => {} });
    }

    void this.router.navigate(['/login'], { queryParams: { reason } });
  }
}
