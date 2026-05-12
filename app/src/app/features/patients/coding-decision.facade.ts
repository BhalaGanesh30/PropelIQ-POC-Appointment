import {
  DestroyRef,
  Injectable,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { CodingDecisionService } from './coding-decision.service';
import type { DecisionEntry, ModifyRequestDto } from '../../shared/models/decision-state.model';

/**
 * Signal-based facade for the Accept / Modify / Reject decision workflow (US_051 / SCR-017).
 *
 * Provided at the `CodingSuggestionPanelComponent` level so decision state is
 * scoped to the panel lifecycle and resets when the panel unmounts.
 *
 * Usage:
 * 1. Panel calls `registerDecisions([...])` after suggestions are loaded.
 * 2. Cards read per-card state via `decisions()[decisionId]`.
 * 3. Cards call `accept()`, `modify()`, or `reject()` on user interaction.
 * 4. `CodingDecisionSummaryBarComponent` reads aggregate counts.
 *
 * Signal map key: `decisionId` string.
 * Missing key means the card state was never registered — treated as 'pending'.
 */
@Injectable()
export class CodingDecisionFacade {
  private readonly service = inject(CodingDecisionService);
  private readonly destroyRef = inject(DestroyRef);

  /** Per-card decision entries keyed by decisionId. */
  readonly decisions = signal<Record<string, DecisionEntry>>({});

  /** Number of decisions still in 'pending' state (AC-4). */
  readonly pendingCount = computed(
    () => Object.values(this.decisions()).filter(e => e.state === 'pending').length,
  );

  /** True when all registered decisions have been resolved (AC-4). */
  readonly allDecided = computed(
    () =>
      Object.keys(this.decisions()).length > 0 && this.pendingCount() === 0,
  );

  readonly acceptedCount = computed(
    () => Object.values(this.decisions()).filter(e => e.state === 'accepted').length,
  );

  readonly modifiedCount = computed(
    () => Object.values(this.decisions()).filter(e => e.state === 'modified').length,
  );

  readonly rejectedCount = computed(
    () => Object.values(this.decisions()).filter(e => e.state === 'rejected').length,
  );

  /** List of entries still in 'pending' state — used by PendingSubmissionBlockBannerComponent (AC-4). */
  readonly pendingEntries = computed(
    () =>
      Object.entries(this.decisions())
        .filter(([, e]) => e.state === 'pending')
        .map(([id, e]) => ({ decisionId: id, code: e.finalCode, description: e.finalDescription })),
  );

  // ── Registration ────────────────────────────────────────────────────────────

  /**
   * Register all suggestion decision IDs as 'pending' at panel load time.
   * Existing entries are preserved — calling this again (e.g. retry) is safe.
   */
  registerDecisions(
    items: ReadonlyArray<{ decisionId: string; code: string; description: string }>,
  ): void {
    this.decisions.update(current => {
      const next = { ...current };
      for (const item of items) {
        if (!next[item.decisionId]) {
          next[item.decisionId] = {
            state: 'pending',
            finalCode: item.code,
            finalDescription: item.description,
          };
        }
      }
      return next;
    });
  }

  // ── Actions ─────────────────────────────────────────────────────────────────

  /**
   * Accept the AI suggestion as-is (AC-1).
   * Calls POST /api/v1/coding-decisions/{id}/accept; updates state on success.
   */
  accept(decisionId: string, code: string, description: string): void {
    this.service
      .accept(decisionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this._setState(decisionId, 'accepted', code, description);
        },
      });
  }

  /**
   * Save a modified code replacing the AI suggestion (AC-2).
   * Calls PATCH /api/v1/coding-decisions/{id}/modify; updates state on success.
   */
  modify(decisionId: string, req: ModifyRequestDto): void {
    this.service
      .modify(decisionId, req)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this._setState(decisionId, 'modified', req.finalCode, req.finalDescription);
        },
      });
  }

  /**
   * Reject the AI suggestion (AC-3).
   * Calls POST /api/v1/coding-decisions/{id}/reject; updates state on success.
   */
  reject(decisionId: string): void {
    this.service
      .reject(decisionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this._setState(decisionId, 'rejected', '', '');
        },
      });
  }

  // ── Internal ────────────────────────────────────────────────────────────────

  private _setState(
    decisionId: string,
    state: DecisionEntry['state'],
    finalCode: string,
    finalDescription: string,
  ): void {
    this.decisions.update(current => ({
      ...current,
      [decisionId]: { state, finalCode, finalDescription },
    }));
  }
}
