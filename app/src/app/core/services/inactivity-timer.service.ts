import { Injectable, NgZone, OnDestroy, inject, signal } from '@angular/core';
import { Subject, fromEvent, merge } from 'rxjs';
import { throttleTime, takeUntil } from 'rxjs/operators';
import { AuthService } from '../../features/auth/services/auth.service';

const WARNING_THRESHOLD_MS = 13 * 60 * 1000; // 13 minutes
const COUNTDOWN_DURATION_S = 120;            // 2-minute countdown
const ACTIVITY_THROTTLE_MS = 30_000;         // debounce activity events
const BROADCAST_CHANNEL_NAME = 'propeliq_session_activity';

/**
 * Tracks user inactivity and drives the session-timeout flow (us_017).
 *
 * Lifecycle:
 *   - call `start()` after a successful login
 *   - At 13 min idle → `showWarning` → 2-min countdown
 *   - `resetTimer()` → called by "Extend Session" button (AC-4)
 *   - countdown → 0 → `forceLogout('session-expired')` (AC-2)
 *   - `stop()` → called on manual logout or SignalR displacement (AC-3)
 *
 * Cross-tab sync: a `BroadcastChannel` message is posted on activity so other
 * same-origin tabs share the same inactivity window (edge case: tab duplication).
 */
@Injectable({ providedIn: 'root' })
export class InactivityTimerService implements OnDestroy {
  private readonly ngZone = inject(NgZone);
  private readonly authService = inject(AuthService);

  /** True when the 2-minute warning modal should be visible (AC-1). */
  readonly showWarning = signal(false);

  /** Seconds remaining in the 2-minute countdown. */
  readonly countdownSeconds = signal(COUNTDOWN_DURATION_S);

  private readonly destroy$ = new Subject<void>();
  private warningTimer: ReturnType<typeof setTimeout> | null = null;
  private countdownInterval: ReturnType<typeof setInterval> | null = null;
  private channel: BroadcastChannel | null = null;
  private isRunning = false;

  /** Start inactivity tracking. Must be called when the user is authenticated. */
  start(): void {
    if (this.isRunning) return;
    this.isRunning = true;

    // Cross-tab sync — activity in any tab resets all tabs.
    if (typeof BroadcastChannel !== 'undefined') {
      this.channel = new BroadcastChannel(BROADCAST_CHANNEL_NAME);
      this.channel.onmessage = (event: MessageEvent<string>) => {
        if (event.data === 'activity') {
          this.resetTimerInternal(/* broadcast= */ false);
        }
      };
    }

    // Listen to DOM activity outside Angular zone to avoid unnecessary CD cycles.
    this.ngZone.runOutsideAngular(() => {
      merge(
        fromEvent(document, 'mousemove'),
        fromEvent(document, 'keydown'),
        fromEvent(document, 'scroll', { passive: true }),
        fromEvent(document, 'touchstart', { passive: true }),
        fromEvent(document, 'click'),
      )
        .pipe(throttleTime(ACTIVITY_THROTTLE_MS), takeUntil(this.destroy$))
        .subscribe(() => this.onUserActivity());
    });

    this.startWarningTimer();
  }

  /** Stop all timers and close the broadcast channel. Call on logout. */
  stop(): void {
    this.isRunning = false;
    this.clearTimers();
    this.showWarning.set(false);
    this.countdownSeconds.set(COUNTDOWN_DURATION_S);
    this.channel?.close();
    this.channel = null;
    this.destroy$.next();
  }

  /**
   * Reset the inactivity timer to 15 minutes from now (AC-4).
   * Called by the "Extend Session" button in the modal.
   */
  resetTimer(): void {
    this.resetTimerInternal(/* broadcast= */ true);
  }

  // ── Private ───────────────────────────────────────────────────────────────

  private onUserActivity(): void {
    // Ignore activity while the warning is visible — user must choose an action.
    if (!this.showWarning()) {
      this.resetTimerInternal(/* broadcast= */ true);
    }
  }

  private resetTimerInternal(broadcast: boolean): void {
    this.clearTimers();
    this.ngZone.run(() => {
      this.showWarning.set(false);
      this.countdownSeconds.set(COUNTDOWN_DURATION_S);
    });
    this.startWarningTimer();

    if (broadcast) {
      this.channel?.postMessage('activity');
    }
  }

  private startWarningTimer(): void {
    this.warningTimer = setTimeout(() => {
      this.ngZone.run(() => {
        this.showWarning.set(true);
        this.startCountdown();
      });
    }, WARNING_THRESHOLD_MS);
  }

  private startCountdown(): void {
    this.countdownSeconds.set(COUNTDOWN_DURATION_S);

    this.countdownInterval = setInterval(() => {
      this.ngZone.run(() => {
        const remaining = this.countdownSeconds() - 1;
        this.countdownSeconds.set(remaining);

        if (remaining <= 0) {
          this.stop();
          this.authService.forceLogout('session-expired');
        }
      });
    }, 1000);
  }

  private clearTimers(): void {
    if (this.warningTimer !== null) {
      clearTimeout(this.warningTimer);
      this.warningTimer = null;
    }
    if (this.countdownInterval !== null) {
      clearInterval(this.countdownInterval);
      this.countdownInterval = null;
    }
  }

  ngOnDestroy(): void {
    this.stop();
    this.destroy$.complete();
  }
}
