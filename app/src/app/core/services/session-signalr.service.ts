import { Injectable, NgZone, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { TokenStorageService } from './token-storage.service';
import { AuthService } from '../../features/auth/services/auth.service';
import { InactivityTimerService } from './inactivity-timer.service';

/**
 * Maintains an authenticated SignalR connection to `/hubs/session`.
 * Listens for server-pushed session lifecycle events (us_017 AC-3):
 *   - `SessionEnded`: fired when a second login displaces this session.
 *
 * Uses automatic reconnect with exponential back-off so short network blips
 * do not immediately log the user out.
 */
@Injectable({ providedIn: 'root' })
export class SessionSignalRService {
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly authService = inject(AuthService);
  private readonly inactivityTimer = inject(InactivityTimerService);
  private readonly ngZone = inject(NgZone);

  private connection: signalR.HubConnection | null = null;

  /** Connect to the session hub. Must be called after a successful login. */
  start(): void {
    const accessToken = this.tokenStorage.getAccessToken();
    // Do not connect if unauthenticated or already connected.
    if (!accessToken || this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/session', {
        // Re-read the token on each reconnect attempt so refreshed JWTs are used.
        accessTokenFactory: () => this.tokenStorage.getAccessToken() ?? '',
      })
      // Retry intervals: immediate, 2 s, 5 s, 10 s, 30 s
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    // AC-3: another device logged in — stop local session services and redirect.
    this.connection.on('SessionEnded', (_message: string) => {
      this.ngZone.run(() => {
        this.inactivityTimer.stop();
        this.stop();
        this.authService.forceLogout('session-ended');
      });
    });

    this.connection
      .start()
      .catch((err: unknown) =>
        console.warn('[SessionSignalR] Connection failed:', err),
      );
  }

  /** Disconnect from the session hub. Call on logout. */
  stop(): void {
    this.connection?.stop().catch(() => {});
    this.connection = null;
  }
}
