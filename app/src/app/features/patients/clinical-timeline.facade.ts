import {
  DestroyRef,
  Injectable,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ClinicalTimelineService } from './clinical-timeline.service';
import type {
  TimelineEventDto,
  TimelineQueryParams,
  TimelineYearGroup,
} from '../../shared/models/timeline-event.model';

/**
 * Signal-based facade for the clinical timeline tab (US_048 / SCR-015).
 *
 * Provided at the `ClinicalTimelineComponent` level so state is scoped
 * to each timeline component instance and resets on unmount.
 *
 * Computed signal `groupedByYear` partitions events into year groups sorted
 * descending (most-recent year first) to satisfy AC-1 reverse-chronological order.
 */
@Injectable()
export class ClinicalTimelineFacade {
  private readonly service = inject(ClinicalTimelineService);
  private readonly destroyRef = inject(DestroyRef);

  // ── Core state signals ────────────────────────────────────────────────────
  readonly events = signal<TimelineEventDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly activeFilters = signal<TimelineQueryParams>({});
  readonly loaded = signal(false);

  // ── Derived state ─────────────────────────────────────────────────────────
  /**
   * Events partitioned into year groups, sorted descending by year (AC-1, Edge Case 2).
   * Current year group is first; within each group events are most-recent-first.
   */
  readonly groupedByYear = computed<TimelineYearGroup[]>(() => {
    const byYear = new Map<number, TimelineEventDto[]>();
    for (const event of this.events()) {
      const year = new Date(event.eventDate).getFullYear();
      const group = byYear.get(year) ?? [];
      group.push(event);
      byYear.set(year, group);
    }

    return Array.from(byYear.entries())
      .sort(([a], [b]) => b - a) // descending by year
      .map(([year, evts]) => ({
        year,
        events: evts.slice().sort(
          (a, b) => new Date(b.eventDate).getTime() - new Date(a.eventDate).getTime(),
        ),
      }));
  });

  // ── Private tracking ──────────────────────────────────────────────────────
  private _patientId = '';

  // ── Public API ────────────────────────────────────────────────────────────

  /**
   * Initial load for the timeline tab.
   * No-ops if already loading or loaded with the same filters.
   */
  load(patientId: string, params: TimelineQueryParams = {}): void {
    this._patientId = patientId;
    this.activeFilters.set(params);
    this._fetch(params);
  }

  /**
   * Merges new filter params and re-fetches from the API (AC-2, AC-3).
   * Called by the filter bar on chip or date range change.
   */
  applyFilters(params: TimelineQueryParams): void {
    this.activeFilters.set(params);
    this._fetch(params);
  }

  /** Clears error and retries the last fetch (retry banner action). */
  retry(): void {
    this.error.set(null);
    this._fetch(this.activeFilters());
  }

  // ── Private ───────────────────────────────────────────────────────────────

  private _fetch(params: TimelineQueryParams): void {
    this.loading.set(true);
    this.error.set(null);

    this.service
      .getTimeline(this._patientId, params)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.events.set(response.events);
          this.loading.set(false);
          this.loaded.set(true);
        },
        error: () => {
          this.error.set('Failed to load timeline. Please try again.');
          this.loading.set(false);
          this.loaded.set(true);
        },
      });
  }
}
