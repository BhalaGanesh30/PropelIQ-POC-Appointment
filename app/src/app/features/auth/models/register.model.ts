/** Payload sent to POST /auth/register — aligns to backend RegisterRequest. */
export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
}

/** Payload sent to POST /auth/send-otp — aligns to backend SendOtpRequest. */
export interface SendOtpRequest {
  email: string;
}

/** Payload sent to POST /auth/verify-otp — aligns to backend VerifyOtpRequest. */
export interface VerifyOtpRequest {
  email: string;
  otp: string;
}

/** Response from POST /auth/register (202 Accepted body). */
export interface RegisterResponse {
  message: string;
}

export type VerificationMethod = 'email' | 'phone';

// ── Login / JWT DTOs ─────────────────────────────────────────────────────────

/** Credentials submitted to POST /auth/login. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** Returned on successful login or token refresh. */
export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  /** Access token lifetime in seconds (900 = 15 minutes, NFR-008). */
  expiresIn: number;
  /** Role-appropriate dashboard path for client-side redirect (AC-1). */
  redirectUrl?: string;
  /** Opaque server-side session token for session/extend calls (us_017). */
  sessionToken?: string;
}

/** Payload for POST /auth/refresh — rotates the access + refresh token pair. */
export interface RefreshRequest {
  accessToken: string;
  refreshToken: string;
}

/** Payload for POST /auth/logout — triggers server-side token revocation (AC-4). */
export interface LogoutRequest {
  refreshToken: string;
}
