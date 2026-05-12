import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { DatePipe, LowerCasePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { AuditLogDetailComponent } from './audit-log-detail.component';
import { AuditLogFilterComponent } from './audit-log-filter.component';
import { AuditLogApiService } from './audit-log-api.service';
import { AuditLogEntry, AuditLogQueryParams } from './models/audit-log.models';

/**
 * SCR-021: Admin Audit Log Viewer — read-only audit trail with filter bar,
 * paginated data table, inline row expansion, and async CSV export.
 *
 * Five states implemented:
 * 1. Default  — filter bar + data table with pagination.
 * 2. Loading  — `mat-progress-bar` indeterminate while fetching.
 * 3. Empty    — "No audit events match filters" message + clear CTA.
 * 4. Error    — retry banner with structured error message.
 * 5. Validation — active filter chips rendered by AuditLogFilterComponent.
 *
 * AC-4: Supports filtering by actor, event type, date range, and resource ID.
 * UXR-201: WCAG AA contrast enforced in styles.
 * UXR-202: Keyboard navigation via Material table rows (tabindex + keyup.enter).
 * UXR-303: Card layout on screens narrower than 768px.
 */
@Component({
  selector: 'app-audit-log-viewer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTableModule,
    LowerCasePipe,
    AuditLogDetailComponent,
    AuditLogFilterComponent,
  ],
  templateUrl: './audit-log-viewer.component.html',
  styleUrl: './audit-log-viewer.component.scss',
})
export class AuditLogViewerComponent implements OnInit {
  private readonly api      = inject(AuditLogApiService);
  private readonly snackBar = inject(MatSnackBar);

  // ── State signals ─────────────────────────────────────────────────────────
  readonly entries      = signal<AuditLogEntry[]>([]);
  readonly totalCount   = signal<number>(0);
  readonly loading      = signal<boolean>(false);
  readonly errorMessage = signal<string>('');
  readonly exporting    = signal<boolean>(false);
  /** auditId of the currently expanded row; null if none. */
  readonly expandedRow  = signal<string | null>(null);

  // ── Table config ──────────────────────────────────────────────────────────
  readonly displayedColumns: string[] = [
    'occurredAt', 'actorName', 'eventType', 'targetEntityType', 'targetEntityId',
  ];

  private currentParams: AuditLogQueryParams = { page: 0, pageSize: 25 };

  ngOnInit(): void {
    this.loadData();
  }

  // ── Event handlers ────────────────────────────────────────────────────────

  onFiltersChanged(params: AuditLogQueryParams): void {
    this.currentParams = { ...params, page: 0 };
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.currentParams = {
      ...this.currentParams,
      page:     event.pageIndex,
      pageSize: event.pageSize,
    };
    this.loadData();
  }

  /** Toggle inline detail expansion for a table row. */
  toggleRow(auditId: string): void {
    this.expandedRow.set(this.expandedRow() === auditId ? null : auditId);
  }

  retryLoad(): void {
    this.errorMessage.set('');
    this.loadData();
  }

  /** Trigger async CSV export and poll until the download is ready (edge case 2). */
  exportCsv(): void {
    this.exporting.set(true);

    this.api.triggerExport(this.currentParams).subscribe({
      next: ({ jobId }) => this.beginExportPolling(jobId),
      error: () => {
        this.exporting.set(false);
        this.snackBar.open('Export failed to start. Please try again.', 'Dismiss', {
          duration: 5000,
        });
      },
    });
  }

  // ── Private helpers ───────────────────────────────────────────────────────

  private loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.expandedRow.set(null);

    this.api.query(this.currentParams).subscribe({
      next: (items) => {
        this.entries.set(items);
        this.totalCount.set(items.length);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set(
          'Failed to load audit logs. Check your connection or contact support.',
        );
        this.loading.set(false);
      },
    });
  }

  /**
   * Polls the export job every 3 seconds until the server returns 200 (file ready).
   * The server streams the CSV directly on GET /export/{jobId} when status is complete,
   * so we open the URL in a new tab once we receive the non-202 response.
   */
  private beginExportPolling(jobId: string): void {
    const downloadUrl = this.api.getExportDownloadUrl(jobId);
    let attempts = 0;
    const maxAttempts = 60; // 3 min maximum polling window

    const intervalId = setInterval(() => {
      attempts++;

      // Open the URL — browser will receive either 202 (ignored) or 200 (file download).
      // We rely on a HEAD-like approach: try fetching the status endpoint.
      fetch(downloadUrl, { method: 'HEAD' }).then((response) => {
        if (response.status === 200) {
          clearInterval(intervalId);
          this.exporting.set(false);
          window.open(downloadUrl, '_blank', 'noopener,noreferrer');
          this.snackBar.open('Export ready — download started.', 'Dismiss', { duration: 4000 });
        } else if (attempts >= maxAttempts) {
          clearInterval(intervalId);
          this.exporting.set(false);
          this.snackBar.open('Export timed out. Please try again.', 'Dismiss', { duration: 5000 });
        }
      }).catch(() => {
        clearInterval(intervalId);
        this.exporting.set(false);
        this.snackBar.open('Export polling failed. Please try again.', 'Dismiss', { duration: 5000 });
      });
    }, 3000);
  }

  /** Returns the detail payload for the given entry as a generic object. */
  getDetailPayload(entry: AuditLogEntry): Record<string, unknown> {
    return (entry.metadata ?? {}) as Record<string, unknown>;
  }
}
