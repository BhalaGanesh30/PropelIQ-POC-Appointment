import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, finalize } from 'rxjs';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';

import { InsuranceReportService } from './insurance-report.service';
import { InsuranceVerificationRecord, InsuranceVerificationStatus } from '../../shared/models/insurance-verification-record.model';
import { VerificationReportPagedResult } from '../../shared/models/verification-report-paged-result.model';

/** Number of records per page by default (server-side, Edge Case 1). */
const DEFAULT_PAGE_SIZE = 25;

/** Badge variant map for UXR-404 colour semantics. */
const STATUS_BADGE: Record<InsuranceVerificationStatus, { label: string; cssClass: string }> = {
  SoftValidated:     { label: 'Soft Validated',     cssClass: 'badge-success' },
  ValidationPending: { label: 'Validation Pending', cssClass: 'badge-warning' },
  ValidationFailed:  { label: 'Validation Failed',  cssClass: 'badge-error'   },
  Warning:           { label: 'Warning',             cssClass: 'badge-warning' },
};

/** Status filter options shown in the dropdown. */
interface StatusOption {
  value: string | null;
  label: string;
}

const STATUS_OPTIONS: StatusOption[] = [
  { value: null,                label: 'All Statuses'       },
  { value: 'SoftValidated',     label: 'Soft Validated'     },
  { value: 'ValidationFailed',  label: 'Validation Failed'  },
  { value: 'ValidationPending', label: 'Validation Pending' },
];

/**
 * Insurance Verification Report (EP-005 US_039 SCR-028 sub-view).
 *
 * Staff-only data table showing all patient insurance verification records with
 * status filtering, server-side pagination, and PDF/CSV export.
 *
 * AC-1: All records with validation status displayed on load.
 * AC-2: Status filter renders results within 500 ms (Redis-cached API).
 * AC-3: PDF export downloads filtered records as a PDF file.
 * AC-4: CSV export downloads filtered records for billing import.
 * Edge Case 1: Exports include all filtered records (full dataset, not current page).
 * Edge Case 2: Route is guarded for Staff/Admin only (roleGuard in app.routes.ts).
 */
@Component({
  selector: 'app-insurance-verification-report',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    DatePipe,
    MatTableModule,
    MatSortModule,
    MatSelectModule,
    MatFormFieldModule,
    MatChipsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  templateUrl: './insurance-verification-report.component.html',
  styleUrls: ['./insurance-verification-report.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InsuranceVerificationReportComponent implements OnInit {
  private readonly reportService = inject(InsuranceReportService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  // ── State signals ──────────────────────────────────────────────────────────
  readonly records = signal<InsuranceVerificationRecord[]>([]);
  readonly totalCount = signal(0);
  readonly currentPage = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  readonly selectedStatus = signal<string | null>(null);
  readonly isLoading = signal(false);
  readonly loadError = signal(false);
  readonly isExportingPdf = signal(false);
  readonly isExportingCsv = signal(false);

  // ── Static config ──────────────────────────────────────────────────────────
  readonly statusOptions = STATUS_OPTIONS;
  readonly displayedColumns = [
    'patientName',
    'providerName',
    'policyNumber',
    'validationStatus',
    'validatedAt',
  ];

  /** Computed: total pages for the current result set. */
  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize()));
  }

  /** Computed: last record index on the current page (capped at totalCount). */
  pageEndIndex(): number {
    return Math.min(this.currentPage() * this.pageSize(), this.totalCount());
  }

  /** Computed: visible page numbers for the pagination bar. */
  get visiblePages(): number[] {
    const total = this.totalPages;
    const current = this.currentPage();
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    const pages: number[] = [1];
    if (current > 3) pages.push(-1); // ellipsis
    const start = Math.max(2, current - 1);
    const end   = Math.min(total - 1, current + 1);
    for (let p = start; p <= end; p++) pages.push(p);
    if (current < total - 2) pages.push(-1); // ellipsis
    pages.push(total);
    return pages;
  }

  /** Label + CSS class for a validation status. */
  badgeFor(status: InsuranceVerificationStatus) {
    return STATUS_BADGE[status] ?? { label: status, cssClass: 'badge-info' };
  }

  /** Label for the active status chip. */
  get activeFilterLabel(): string | null {
    const s = this.selectedStatus();
    return s ? (STATUS_OPTIONS.find(o => o.value === s)?.label ?? s) : null;
  }

  ngOnInit(): void {
    this.loadPage(1);
  }

  // ── Event handlers ─────────────────────────────────────────────────────────

  onStatusChange(status: string | null): void {
    this.selectedStatus.set(status);
    this.loadPage(1);
  }

  clearFilter(): void {
    this.selectedStatus.set(null);
    this.loadPage(1);
  }

  onSortChange(_sort: Sort): void {
    // Sort is currently advisory (sent via query params in a future iteration).
    // Reload from page 1 when sort changes.
    this.loadPage(1);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.loadPage(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.loadPage(1);
  }

  // ── Export ─────────────────────────────────────────────────────────────────

  exportPdf(): void {
    if (this.isExportingPdf()) return;
    this.isExportingPdf.set(true);

    this.reportService.exportPdf(this.selectedStatus())
      .pipe(
        finalize(() => this.isExportingPdf.set(false)),
        catchError(() => {
          this.snackBar.open('PDF export failed. Please try again.', 'Dismiss', { duration: 5000 });
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(blob => {
        this.triggerDownload(blob, 'insurance-verification-report.pdf');
        this.snackBar.open('PDF downloaded successfully.', undefined, { duration: 3000 });
      });
  }

  exportCsv(): void {
    if (this.isExportingCsv()) return;
    this.isExportingCsv.set(true);

    this.reportService.exportCsv(this.selectedStatus())
      .pipe(
        finalize(() => this.isExportingCsv.set(false)),
        catchError(() => {
          this.snackBar.open('CSV export failed. Please try again.', 'Dismiss', { duration: 5000 });
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(blob => {
        this.triggerDownload(blob, 'insurance-verification-report.csv');
        this.snackBar.open('CSV downloaded successfully.', undefined, { duration: 3000 });
      });
  }

  // ── Private helpers ────────────────────────────────────────────────────────

  private loadPage(page: number): void {
    this.isLoading.set(true);
    this.loadError.set(false);
    this.currentPage.set(page);

    this.reportService.getReport(this.selectedStatus(), page, this.pageSize())
      .pipe(
        finalize(() => this.isLoading.set(false)),
        catchError(() => {
          this.loadError.set(true);
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result: VerificationReportPagedResult) => {
        this.records.set(result.records);
        this.totalCount.set(result.totalCount);
      });
  }

  private triggerDownload(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
