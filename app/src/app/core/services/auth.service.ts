import { Injectable } from '@angular/core';
import { TokenStorageService } from './token-storage.service';

/**
 * Thin auth-state facade used by the core auth guard.
 * Delegates to `TokenStorageService` for token inspection.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private readonly tokenStorage: TokenStorageService) {}

  isAuthenticated(): boolean {
    return this.tokenStorage.isAuthenticated() && !this.tokenStorage.isTokenExpired();
  }
}
