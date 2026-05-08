import {
  Injectable,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';

import { PatientProfileService } from './patient-profile.service';
import type { ClinicalFactDto, PartialSourceDto, PatientHeaderDto } from '../../shared/models/clinical-fact.model';

export type TabId = 'summary' | 'timeline' | 'documents' | 'insurance' | 'coding' | 'conflicts';

/** Per-tab state tracked by the facade. */
interface TabState {
  loading: boolean;
  error: string | null;
  facts: ClinicalFactDto[];
  partialSources: PartialSourceDto[];
  loaded: boolean;
}

function emptyTabState(): TabState {
  return { loading: false, error: null, facts: [], partialSources: [], loaded: false };
}

/**
 * Facade coordinating per-tab lazy load for the 360° patient profile (SCR-014).
 *
 * Design:
 * - Each tab has its own Signal state (loading, error, facts, partialSources).
 * - Only the active tab triggers an API call (section-based lazy loading — AC-1).
 * - On error, sets `error` with the source name for the warning banner (Edge Case 1).
 * - `reloadTab()` clears `loaded` flag and retriggers the fetch.
 */
@Injectable()
export class PatientProfileFacade {
  private readonly service = inject(PatientProfileService);
  private readonly destroyRef = inject(DestroyRef);

  // ── Signals ─────────────────────────────────────────────────────────────────
  readonly patientHeader = signal<PatientHeaderDto | null>(null);
  readonly headerLoading = signal(false);
  readonly headerError = signal<string | null>(null);

  private readonly _tabStates = signal<Record<TabId, TabState>>({
    summary:   emptyTabState(),
    timeline:  emptyTabState(),
    documents: emptyTabState(),
    insurance: emptyTabState(),
    coding:    emptyTabState(),
    conflicts: emptyTabState(),
  });

  /** Returns reactive state for a given tab. */
  tabState(tab: TabId) {
    return computed(() => this._tabStates()[tab]);
  }

  // ── Initialization ───────────────────────────────────────────────────────────
  private _patientId = '';

  init(patientId: string): void {
    this._patientId = patientId;
    this._loadHeader(patientId);
    // Pre-load summary tab immediately.
    this.activateTab('summary');
  }

  /** Called when the user switches to a tab (lazy loading per tab). */
  activateTab(tab: TabId): void {
    // Only tabs that have no data yet trigger a request.
    const state = this._tabStates()[tab];
    if (state.loaded || state.loading) return;

    // Insurance, coding, and conflicts tabs are stubs — no API call needed.
    // Timeline is now a real component (US_048) that manages its own data via ClinicalTimelineFacade.
    if (tab === 'insurance' || tab === 'coding' || tab === 'conflicts' || tab === 'timeline') {
      this._patchTab(tab, { loaded: true });
      return;
    }

    // Documents tab links to document library — no inline fetch.
    if (tab === 'documents') {
      this._patchTab(tab, { loaded: true });
      return;
    }

    this._loadTab(tab);
  }

  /** Clears error and re-fetches a tab (retry from error/partial-data banner). */
  reloadTab(tab: TabId): void {
    this._patchTab(tab, { loaded: false, error: null });
    this.activateTab(tab);
  }

  // ── Private helpers ──────────────────────────────────────────────────────────
  private _loadHeader(patientId: string): void {
    this.headerLoading.set(true);
    this.headerError.set(null);

    this.service
      .getPatientHeader(patientId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (header) => {
          this.patientHeader.set(header);
          this.headerLoading.set(false);
        },
        error: () => {
          this.headerError.set('Failed to load patient information.');
          this.headerLoading.set(false);
        },
      });
  }

  private _loadTab(tab: TabId): void {
    this._patchTab(tab, { loading: true, error: null });

    this.service
      .getProfile(this._patientId, tab)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          this._patchTab(tab, {
            loading: false,
            loaded: true,
            facts: profile.facts,
            partialSources: profile.partialSources,
            error: null,
          });
        },
        error: (err: unknown) => {
          const message =
            err instanceof Error ? err.message : 'Failed to load clinical data.';
          this._patchTab(tab, {
            loading: false,
            loaded: true,
            error: message,
          });
        },
      });
  }

  private _patchTab(tab: TabId, patch: Partial<TabState>): void {
    this._tabStates.update((states) => ({
      ...states,
      [tab]: { ...states[tab], ...patch },
    }));
  }

  /**
   * Replaces a single fact in the summary tab Signal after a successful edit or verify
   * (US_047 AC-1/AC-2 — no full page reload required).
   * Called by `ClinicalFactCardComponent` via the `factUpdated` output.
   */
  updateFact(updated: ClinicalFactDto): void {
    this._tabStates.update((states) => {
      const tab = states['summary'];
      const facts = tab.facts.map((f) =>
        f.factId === updated.factId ? { ...f, ...updated } : f,
      );
      return { ...states, summary: { ...tab, facts } };
    });
  }
}
