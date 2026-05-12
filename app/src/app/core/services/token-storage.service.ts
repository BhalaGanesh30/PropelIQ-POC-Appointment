import { Injectable, signal } from '@angular/core';

const ACCESS_TOKEN_KEY = 'propeliq_access_token';
const REFRESH_TOKEN_KEY = 'propeliq_refresh_token';
const SESSION_TOKEN_KEY = 'propeliq_session_token';

/**
 * Manages JWT access-token and refresh-token persistence.
 * Supports localStorage (remember-me) or sessionStorage (session-only).
 * Exposes `isAuthenticated` as a signal so components and guards react
 * reactively to login/logout transitions.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private storage: Storage = localStorage;

  readonly isAuthenticated = signal(false);

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

  saveSessionToken(sessionToken: string): void {
    this.storage.setItem(SESSION_TOKEN_KEY, sessionToken);
  }

  getSessionToken(): string | null {
    return (
      localStorage.getItem(SESSION_TOKEN_KEY) ??
      sessionStorage.getItem(SESSION_TOKEN_KEY)
    );
  }

  getAccessToken(): string | null {
    return (
      localStorage.getItem(ACCESS_TOKEN_KEY) ??
      sessionStorage.getItem(ACCESS_TOKEN_KEY)
    );
  }

  getRefreshToken(): string | null {
    return (
      localStorage.getItem(REFRESH_TOKEN_KEY) ??
      sessionStorage.getItem(REFRESH_TOKEN_KEY)
    );
  }

  clearTokens(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(SESSION_TOKEN_KEY);
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    sessionStorage.removeItem(SESSION_TOKEN_KEY);
    this.isAuthenticated.set(false);
  }

  getDecodedToken(): Record<string, unknown> | null {
    const token = this.getAccessToken();
    if (!token) return null;
    try {
      const payload = token.split('.')[1];
      return JSON.parse(atob(payload)) as Record<string, unknown>;
    } catch {
      return null;
    }
  }

  /** Returns the first role claim from the decoded JWT, or null if absent.
   *  JwtSecurityTokenHandler maps ClaimTypes.Role → "role" in the JWT payload
   *  via DefaultOutboundClaimTypeMap, so we check the short form first and
   *  fall back to the full URI for tokens generated with mapping disabled.
   */
  getUserRole(): string | null {
    const decoded = this.getDecodedToken();
    if (!decoded) return null;
    // Primary: short claim name emitted by JwtSecurityTokenHandler mapping.
    const shortRole = decoded['role'];
    if (shortRole != null) {
      return Array.isArray(shortRole) ? (shortRole[0] as string) : (shortRole as string);
    }
    // Fallback: full URI used when DefaultOutboundClaimTypeMap is cleared.
    const longRole =
      decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    if (longRole != null) {
      return Array.isArray(longRole) ? (longRole[0] as string) : (longRole as string);
    }
    return null;
  }

  isTokenExpired(): boolean {
    const decoded = this.getDecodedToken();
    if (!decoded?.['exp']) return true;
    return Date.now() >= (decoded['exp'] as number) * 1000;
  }
}
