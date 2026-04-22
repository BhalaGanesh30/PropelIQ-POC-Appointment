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

  /** Returns the first role claim from the decoded JWT, or null if absent. */
  getUserRole(): string | null {
    const decoded = this.getDecodedToken();
    if (!decoded) return null;
    // ASP.NET Core Identity serialises roles under this claim type.
    const role =
      decoded[
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
      ];
    return Array.isArray(role) ? (role[0] as string) : ((role as string) ?? null);
  }

  isTokenExpired(): boolean {
    const decoded = this.getDecodedToken();
    if (!decoded?.['exp']) return true;
    return Date.now() >= (decoded['exp'] as number) * 1000;
  }
}
