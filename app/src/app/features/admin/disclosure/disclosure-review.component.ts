import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { DisclosureAdminApiService } from './disclosure-admin-api.service';
import {
  DisclosureRequest,
  DisclosureReport,
  DisclosureStatus,
} from '../../settings/disclosure/models/disclosure.models';

/** All status values for the filter drop-down. */
const ALL_STATUSES: DisclosureStatus[] = [
  'Submitted', 'Compiling', 'PendingReview', 'Approved', 'Delivered', 'Rejected',
];

/**
 * Staff / Admin disclosure review queue (US_057, AC-5).
 *
 * Route: /admin/disclosure-requests
 * Policy: StaffOrAdmin (enforced via roleGuard on the route)
 *
 * Features:
 * - Paginated queue filtered by status (default PendingReview).
 * - Inline report JSON preview panel (expandable row).
 * - Approve / Reject action with optional review notes.
 * - Snack-bar feedback on every action.
 *
 * UXR-303: Card layout below 768 px.
 */
@Component({
  selector: 'app-disclosure-review',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    DatePipe,
    MatButtonModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTableModule,
  ],
  templateUrl: './disclosure-review.component.html',
  styleUrl: './disclosure-review.component.scss',
})
export class DisclosureReviewComponent implements OnInit {
  private readonly api      = inject(DisclosureAdminApiService);
  readonly snackBar = inject(MatSnackBar);

  readonly allStatuses = ALL_STATUSES;

  // ── Filter state ───────────────────────────────────────────────────────────
  readonly filterStatus = signal<DisclosureStatus | null>('PendingReview');

  // ── Table state ────────────────────────────────────────────────────────────
  readonly requests     = signal<DisclosureRequest[]>([]);
  readonly totalCount   = signal(0);
  readonly loading      = signal(false);
  readonly errorMessage = signal('');

  readonly page     = signal(1);
  readonly pageSize = signal(20);

  readonly displayedColumns = ['requestedAt', 'patientId', 'dateRange', 'status', 'actions'];

  // ── Expanded row / report preview ─────────────────────────────────────────
  readonly expandedRow    = signal<DisclosureRequest | null>(null);
  readonly selectedReport = signal<DisclosureReport | null>(null);
  readonly reportLoading  = signal(false);

  // ── Review action state ───────────────────────────────────────────────────
  /** ID of the request currently being actioned (shows a spinner). */
  readonly actioningId  = signal<string | null>(null);
  /** Review notes bound to the inline textarea. */
  readonly reviewNotes  = signal('');

  // ── Lifecycle ─────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.loadRequests();
  }

  // ── Filter ─────────────────────────────────────────────────────────────────

  applyFilter(): void {
    this.page.set(1);
    this.loadRequests();
  }

  // ── Pagination ─────────────────────────────────────────────────────────────

  onPageChange(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.loadRequests();
  }

  // ── Row expansion ─────────────────────────────────────────────────────────

  toggleRow(request: DisclosureRequest): void {
    if (this.expandedRow()?.id === request.id) {
      this.expandedRow.set(null);
      this.selectedReport.set(null);
      return;
    }

    this.expandedRow.set(request);
    this.selectedReport.set(null);

    if (request.status !== 'Submitted' && request.status !== 'Compiling') {
      this.loadReport(request.id);
    }
  }

  isExpanded(request: DisclosureRequest): boolean {
    return this.expandedRow()?.id === request.id;
  }

  // ── Review actions ────────────────────────────────────────────────────────

  approve(request: DisclosureRequest): void {
    this.doReview(request, true);
  }

  reject(request: DisclosureRequest): void {
    if (!this.reviewNotes().trim()) {
      this.snackBar.open('Please provide a reason when rejecting a request.', 'Dismiss', { duration: 5000 });
      return;
    }
    this.doReview(request, false);
  }

  private doReview(request: DisclosureRequest, approved: boolean): void {
    this.actioningId.set(request.id);

    this.api
      .review(request.id, { approved, notes: this.reviewNotes().trim() || null })
      .subscribe({
        next: () => {
          this.actioningId.set(null);
          this.reviewNotes.set('');
          this.expandedRow.set(null);
          this.selectedReport.set(null);
          this.snackBar.open(
            approved ? 'Request approved — patient notified by email.' : 'Request rejected.',
            'Dismiss',
            { duration: 5000 },
          );
          this.loadRequests();
        },
        error: () => {
          this.actioningId.set(null);
          this.snackBar.open('Action failed. Please try again.', 'Dismiss', { duration: 5000 });
        },
      });
  }

  statusColor(status: DisclosureStatus): 'primary' | 'accent' | 'warn' | '' {
    switch (status) {
      case 'Delivered': return 'primary';
      case 'Rejected':  return 'warn';
      case 'PendingReview':
      case 'Approved':
      case 'Compiling': return 'accent';
      default: return '';
    }
  }

  // ── Data loading ───────────────────────────────────────────────────────────

  private loadRequests(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.api.list(this.filterStatus(), this.page(), this.pageSize()).subscribe({
      next: (result) => {
        this.requests.set(result.items);
        this.totalCount.set(result.total);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load disclosure queue.');
        this.loading.set(false);
      },
    });
  }

  private loadReport(requestId: string): void {
    this.reportLoading.set(true);

    this.api.getReport(requestId).subscribe({
      next: (report) => {
        this.selectedReport.set(report);
        this.reportLoading.set(false);
      },
      error: () => {
        this.reportLoading.set(false);
        this.snackBar.open('Failed to load report preview.', 'Dismiss', { duration: 4000 });
      },
    });
  }
}
