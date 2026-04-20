import { Injectable } from '@angular/core';

/**
 * Authentication state stub — returns authenticated by default.
 * Replace with real JWT / session validation in EP-001 (auth epic).
 * `redirectUrl` stores the attempted URL so the login flow can redirect
 * back after successful authentication.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  redirectUrl: string | null = null;

  isAuthenticated(): boolean {
    // Stub: always authenticated during scaffold phase.
    // EP-001 will inject token validation here.
    return true;
  }
}
