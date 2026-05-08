import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, interval } from 'rxjs';
import { catchError, retry, startWith, switchMap, tap } from 'rxjs/operators';
import { QueueEntry } from './models/queue-entry.model';

/** Normal poll cadence (AC-2: refreshes within 5 s on status change; 15 s poll covers this). */
const POLL_INTERVAL_MS = 15_000;
/** Edge Case 1: retry delay when a poll request fails. */
const RECONNECT_DELAY_MS = 10_000;

/**
 * RxJS polling service for the real-time staff queue (EP-004 US_031).
 *
 * Emits fresh QueueEntry[] every POLL_INTERVAL_MS milliseconds.
 * On HTTP error:
 *   - Sets `connectionError` signal so the component can render the
 *     "Reconnecting…" banner (Edge Case 1 / UXR-303).
 *   - Retries the individual HTTP call after RECONNECT_DELAY_MS (10 s)
 *     without resetting the outer 15 s interval.
 * On recovery:
 *   - Clears `connectionError` so the banner is dismissed automatically.
 *
 * Usage: subscribe to `buildPoll$()` in OnInit; unsubscribe in OnDestroy.
 */
@Injectable({ providedIn: 'root' })
export class QueuePollingService {
  private readonly http = inject(HttpClient);

  private readonly _connectionError = signal(false);
  /** Read-only view of the connection-error state for component templates. */
  readonly connectionError = this._connectionError.asReadonly();

  /**
   * Returns an Observable that immediately fetches today's queue entries and
   * then repeats every POLL_INTERVAL_MS milliseconds.
   *
   * Each subscriber gets an independent subscription (cold-like behaviour via
   * `interval`). The component should call this once in `ngOnInit`.
   */
  buildPoll$(): Observable<QueueEntry[]> {
    return interval(POLL_INTERVAL_MS).pipe(
      startWith(0),
      switchMap(() =>
        this.http.get<QueueEntry[]>('/api/v1/queue/today').pipe(
          // Success path: clear any prior connection-error state.
          tap(() => this._connectionError.set(false)),
          // Error path: set flag (shows banner), then re-throw so retry can act.
          catchError((err: unknown) => {
            this._connectionError.set(true);
            throw err;
          }),
          // Edge Case 1: retry this HTTP call after 10 s before giving up for
          // this cycle (outer interval will try again after 15 s regardless).
          retry({ delay: RECONNECT_DELAY_MS }),
        ),
      ),
    );
  }
}
