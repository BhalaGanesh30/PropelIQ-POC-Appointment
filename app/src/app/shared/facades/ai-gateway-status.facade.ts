import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, interval } from 'rxjs';
import { catchError, switchMap, takeUntil } from 'rxjs/operators';

import { AiGatewayStatusService } from '../services/ai-gateway-status.service';

/** Polling interval in milliseconds while the circuit is open (AC-2, AC-3). */
const POLL_INTERVAL_MS = 30_000;

/**
 * Root-level facade for the AI gateway circuit breaker state (US_053, AC-2, AC-3).
 *
 * Provides:
 * - `fallbackActive` — Signal<boolean> consumed by AI-dependent screens via `@if`.
 *   `true` → banner visible; `false` → banner hidden (Edge Case 2).
 * - `initialize()` — called once on `AppComponent.ngOnInit()`.
 *   Fetches status immediately; if the circuit is open/half-open, starts a 30-second
 *   polling loop. Polling auto-stops once the circuit returns `'closed'`.
 *
 * Polling contract (Edge Case 1 — rapid cycling):
 *   The banner reflects the current status truthfully on each poll tick.
 *   No debouncing is applied to the status change — the signal is set immediately.
 *
 * Provided in root so a single shared instance is reused across all screens.
 */
@Injectable({ providedIn: 'root' })
export class AiGatewayStatusFacade {
  private readonly service = inject(AiGatewayStatusService);
  private readonly destroyRef = inject(DestroyRef);

  /** True while the circuit is `open` or `half-open`; false when `closed`. */
  readonly fallbackActive = signal(false);

  /**
   * Subject that emits when polling should stop (circuit returned `'closed'`).
   * A new Subject is created each time `initialize()` starts a polling loop so
   * the previous Subject's `complete()` doesn't suppress subsequent polls.
   */
  private stopPolling$ = new Subject<void>();

  /**
   * Initialise the facade.
   *
   * Called once from `AppComponent.ngOnInit()`. Performs an immediate status
   * check; if fallback is active it starts the 30-second polling loop.
   */
  initialize(): void {
    this.service
      .getStatus()
      .pipe(
        catchError(() => EMPTY),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((status) => {
        this.fallbackActive.set(status.fallbackActive);

        if (status.fallbackActive) {
          this.startPolling();
        }
      });
  }

  // ── Private ────────────────────────────────────────────────────────────────

  /**
   * Starts the 30-second polling loop.
   *
   * Uses `switchMap` over `interval` so each tick replaces any in-flight request.
   * Terminates via `takeUntil(stopPolling$)` when the circuit closes, and via
   * `takeUntilDestroyed` when the root injector is destroyed.
   *
   * HTTP errors during polling are silently swallowed (`catchError → EMPTY`)
   * to prevent the poll stream from completing prematurely on transient failures.
   */
  private startPolling(): void {
    // Reset any previous stop-signal before starting a new polling session.
    this.stopPolling$.complete();
    this.stopPolling$ = new Subject<void>();

    interval(POLL_INTERVAL_MS)
      .pipe(
        switchMap(() =>
          this.service.getStatus().pipe(catchError(() => EMPTY)),
        ),
        takeUntil(this.stopPolling$),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((status) => {
        this.fallbackActive.set(status.fallbackActive);

        if (status.circuitState === 'closed') {
          // Circuit recovered — stop polling (AC-3).
          this.stopPolling$.next();
          this.stopPolling$.complete();
        }
      });
  }
}
