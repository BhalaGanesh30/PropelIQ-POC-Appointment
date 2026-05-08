import {
  DestroyRef,
  Injectable,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ConflictAlertsService } from './conflict-alerts.service';
import type { ConflictAlertDto } from '../../shared/models/conflict-alert.model';

/** Severity sort order — Critical first (AC-1). */
const SEVERITY_ORDER: Record<string, number> = {
  critical: 0,
  high: 1,
  moderate: 2,
  low: 3,
};

/**
 * Signal-based facade for the conflict alerts tab (SCR-016).
 *
 * Provided at the ConflictAlertsComponent level so state resets on unmount.
 * - `pendingCritical`: computed slice of unacknowledged critical alerts (AC-3).
 * - `sortedActiveAlerts`: computed list of unacknowledged alerts sorted by severity.
 * - `acknowledgedAlerts`: acknowledged alerts for the "Resolved" section.
 */
@Injectable()
export class ConflictAlertsFacade {
  private readonly service = inject(ConflictAlertsService);
  private readonly destroyRef = inject(DestroyRef);

  // ── Signals ─────────────────────────────────────────────────────────────────
  readonly alerts = signal<ConflictAlertDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly rulesStale = signal(false);
  readonly loaded = signal(false);

  // ── Computed slices ──────────────────────────────────────────────────────────
  /** Unacknowledged critical alerts that require mandatory acknowledgment (AC-3). */
  readonly pendingCritical = computed(() =>
    this.alerts().filter((a) => a.severity === 'critical' && !a.acknowledged),
  );

  /** All unacknowledged alerts sorted Critical → High → Moderate → Low (AC-1). */
  readonly sortedActiveAlerts = computed(() =>
    this.alerts()
      .filter((a) => !a.acknowledged)
      .slice()
      .sort(
        (a, b) =>
          (SEVERITY_ORDER[a.severity] ?? 99) - (SEVERITY_ORDER[b.severity] ?? 99),
      ),
  );

  /** Acknowledged alerts for the collapsible "Resolved" section. */
  readonly acknowledgedAlerts = computed(() =>
    this.alerts().filter((a) => a.acknowledged),
  );

  // ── Actions ──────────────────────────────────────────────────────────────────

  /** Loads conflict alerts from the API (lazy — called on tab activation). */
  loadConflicts(patientId: string): void {
    if (this.loaded() || this.loading()) return;

    this.loading.set(true);
    this.error.set(null);

    this.service
      .getConflicts(patientId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.alerts.set(response.alerts);
          this.rulesStale.set(response.rulesStale);
          this.loading.set(false);
          this.loaded.set(true);
        },
        error: () => {
          this.error.set('Failed to load conflict alerts. Please try again.');
          this.loading.set(false);
          this.loaded.set(true);
        },
      });
  }

  /** Retries after an error (clears loaded flag so loadConflicts fires again). */
  reload(patientId: string): void {
    this.loaded.set(false);
    this.error.set(null);
    this.loadConflicts(patientId);
  }

  /**
   * Sends acknowledgment to the API and marks the alert locally (optimistic update).
   * If the API call fails the alert remains acknowledged in local state (best-effort audit).
   */
  acknowledge(conflictId: string): void {
    // Optimistic update — mark immediately so UI responds without waiting for API.
    this.alerts.update((alerts) =>
      alerts.map((a) =>
        a.conflictId === conflictId
          ? { ...a, acknowledged: true, acknowledgedAt: new Date().toISOString() }
          : a,
      ),
    );

    this.service
      .acknowledgeConflict(conflictId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        // Server-side audit trail recorded (AC-4). No further UI action needed.
        error: () => {
          // The optimistic update stands even on error (prevents blocking the clinician).
          // The server will surface the failure in its own audit log.
        },
      });
  }
}
