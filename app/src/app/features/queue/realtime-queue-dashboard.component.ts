import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { QueuePollingService } from './queue-polling.service';
import { CheckinActionsComponent } from './checkin-actions.component';
import {
  ALL_QUEUE_STATUSES,
  QUEUE_STATUS_BADGE_COLORS,
  QUEUE_STATUS_LABELS,
  QueueEntry,
  QueueStatus,
} from './models/queue-entry.model';

/** Filter value type — 'ALL' shows every status. */
type StatusFilter = QueueStatus | 'ALL';

/**
 * Real-Time Queue Dashboard (EP-004 US_031).
 *
 * Renders today's appointment queue with color-coded status badges,
 * wait-time estimates, overdue row highlighting, and a status filter.
 *
 * AC-1: Table loaded within 3 s with all status badges and wait-time column.
 * AC-2: Polling via QueuePollingService refreshes data every 15 s.
 * AC-3: Rows with isOverdue=true receive amber highlight + warning indicator.
 * AC-4: Status filter hides non-matching rows reactively via computed signal.
 *
 * Edge Case 1: Reconnecting banner shown when connectionError signal is true.
 * Edge Case 2: mat-table wrapped in a fixed-height scroll container so 100+
 *              rows don't overflow the viewport (virtual scroll feasible via
 *              CDK but requires mat-table rebuild — addressed in a follow-up).
 *
 * UXR-106: Auto-refresh spinner and "every 15 s" hint in the page header.
 * UXR-201/UXR-404: Status badge colours meet WCAG AA 4.5:1 on white.
 * UXR-203: aria-live region announces table updates to screen readers.
 * UXR-301: Responsive via CSS containment — no layout breakage at 375 px.
 * UXR-303: Overdue rows use amber left-border + warning icon.
 */
@Component({
  selector: 'app-realtime-queue-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
    CheckinActionsComponent,
  ],
  templateUrl: './realtime-queue-dashboard.component.html',
  styleUrl: './realtime-queue-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RealtimeQueueDashboardComponent implements OnInit, OnDestroy {
  private readonly pollingService = inject(QueuePollingService);
  private readonly destroy$ = new Subject<void>();

  // ── State signals ──────────────────────────────────────────────────────────
  readonly entries = signal<QueueEntry[]>([]);
  readonly isLoading = signal(true);
  readonly statusFilter = signal<StatusFilter>('ALL');

  /** Forwarded from the service for template binding. */
  readonly connectionError = this.pollingService.connectionError;

  // ── Computed ───────────────────────────────────────────────────────────────
  /** AC-4: Reactively derived subset driven by statusFilter signal. */
  readonly filteredEntries = computed<QueueEntry[]>(() => {
    const filter = this.statusFilter();
    return filter === 'ALL'
      ? this.entries()
      : this.entries().filter((e) => e.status === filter);
  });

  // ── Template constants ─────────────────────────────────────────────────────
  readonly displayedColumns = [
    'patientName',
    'appointmentType',
    'arrivedAt',
    'status',
    'waitTime',
    'actions',
  ];

  readonly filterOptions: ReadonlyArray<{ value: StatusFilter; label: string }> = [
    { value: 'ALL', label: 'All' },
    ...ALL_QUEUE_STATUSES.map((s) => ({ value: s, label: QUEUE_STATUS_LABELS[s] })),
  ];

  // ── Lifecycle ──────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.pollingService
      .buildPoll$()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.entries.set(data);
          this.isLoading.set(false);
        },
        // Error handler is intentionally minimal — QueuePollingService sets
        // connectionError and retries internally; we never expect a terminal
        // error here unless the app is torn down.
        error: () => this.isLoading.set(false),
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Template helpers ───────────────────────────────────────────────────────

  /**
   * Safe badge-colour lookup.
   * Accepts `string` because *matCellDef infers `entry` as `any`, making
   * `entry.status` untyped at the template level. The cast + fallback keeps
   * this both type-safe and resilient to unexpected values.
   */
  getStatusColor(status: string): string {
    return QUEUE_STATUS_BADGE_COLORS[status as QueueStatus] ?? '#616161';
  }

  /** Safe status-label lookup — see getStatusColor for why `string` is used. */
  getStatusLabel(status: string): string {
    return QUEUE_STATUS_LABELS[status as QueueStatus] ?? status;
  }

  /** Returns the wait-time display string for a given queue entry. */
  getWaitLabel(entry: QueueEntry): string {
    if (entry.status === 'Completed' || entry.status === 'NoShow') return '—';
    if (entry.isOverdue) return `${entry.actualWaitMinutes} min (overdue)`;
    return `~${entry.estimatedWaitMinutes} min`;
  }

  /**
   * Replaces the matching entry in the queue signal with the server-updated
   * entry returned by CheckinActionsComponent after a successful PATCH.
   * No full list refresh needed — the entry is updated in-place (UXR-106).
   */
  onStateChanged(updated: QueueEntry): void {
    this.entries.update((list) =>
      list.map((e) => (e.appointmentId === updated.appointmentId ? updated : e)),
    );
  }
}
