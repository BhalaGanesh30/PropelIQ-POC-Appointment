import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subject, takeUntil } from 'rxjs';

import { OverrideService, OverrideAuditLogParams } from './override.service';
import { OverrideAuditEntry } from '../../shared/models/override-audit-entry.model';

/**
 * Override Audit Log component (EP-004 US_034 AC-4).
 *
 * Displays an admin-facing table of all scheduling override events.
 * Supports date range filtering and lazy loads on init.
 *
 * AC-4: Override reason and staff identity are surfaced in the log table.
 * NFR-010: All override events are immutably recorded on the server and
 *          surfaced here for compliance review.
 *
 * UXR-201: WCAG AA contrast on all table rows.
 * UXR-202: Full keyboard navigation via MatTable + MatDatepicker.
 */
@Component({
  selector: 'app-override-audit-log',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './override-audit-log.component.html',
  styleUrl: './override-audit-log.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverrideAuditLogComponent implements OnInit, OnDestroy {
  private readonly overrideService = inject(OverrideService);
  private readonly destroy$        = new Subject<void>();

  // ── Table ──────────────────────────────────────────────────────────────
  readonly displayedColumns: string[] = [
    'timestamp',
    'actorName',
    'actorRole',
    'constraint',
    'reason',
  ];

  // ── Signals ────────────────────────────────────────────────────────────
  readonly entries   = signal<OverrideAuditEntry[]>([]);
  readonly isLoading = signal(false);
  readonly loadError = signal<string | null>(null);

  // ── Date range filter form ─────────────────────────────────────────────
  readonly filterForm = inject(FormBuilder).nonNullable.group({
    from: [null as Date | null],
    to:   [null as Date | null],
  });

  ngOnInit(): void {
    this.loadEntries();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  applyFilter(): void {
    this.loadEntries();
  }

  clearFilter(): void {
    this.filterForm.reset();
    this.loadEntries();
  }

  private loadEntries(): void {
    this.isLoading.set(true);
    this.loadError.set(null);

    const { from, to } = this.filterForm.value;
    const params: OverrideAuditLogParams = {};

    if (from) { params.from = (from as Date).toISOString(); }
    if (to)   { params.to   = (to   as Date).toISOString(); }

    this.overrideService
      .getOverrideAuditLog(params)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.entries.set(data);
          this.isLoading.set(false);
        },
        error: (err: { error?: { message?: string } }) => {
          this.loadError.set(err?.error?.message ?? 'Failed to load audit log.');
          this.isLoading.set(false);
        },
      });
  }
}
