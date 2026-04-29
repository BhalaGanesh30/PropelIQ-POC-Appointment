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
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, debounceTime, switchMap, takeUntil, tap } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppointmentApiService } from './appointment-api.service';
import { AppointmentHistoryApiService } from './appointment-history-api.service';
import {
  CancelDialogComponent,
  CancelDialogData,
  CancelDialogResult,
} from './cancel-dialog.component';
import { AppointmentDetailDialogComponent } from './appointment-detail-dialog.component';
import {
  AppointmentHistoryFilter,
  AppointmentHistoryItem,
  APPOINTMENT_STATUSES,
} from './models/appointment-history.models';
import { TokenStorageService } from '../../core/services/token-storage.service';
import { BookingApiService } from '../scheduling/services/booking-api.service';

/**
 * Appointment History page — SCR-007.
 *
 * States: Default (filter bar + table), Loading (skeleton rows),
 * Empty (no-results CTA), Error (retry banner), Validation (cancel dialog + toast).
 *
 * AC-1: History list sorted date descending via new GET /api/v1/appointmenthistory.
 * AC-2: Status filter with 300 ms debounce + switchMap cancellation.
 * AC-3: Date range filter — same debounce pipeline.
 * AC-4: "Export PDF" blob download from GET /api/v1/appointmenthistory/export.
 * Edge case: Pagination at 20 per page; PDF covers all filtered records.
 * Edge case: Empty → "No appointments found. Book your first appointment."
 * UXR-202: keyboard-navigable filter controls with visible focus indicators.
 * UXR-301: responsive 375px (card list) / 768px (tablet) / 1440px (full table).
 * UXR-303: table switches to card layout below 768px.
 * UXR-304: touch targets ≥ 44×44px on mobile.
 * UXR-501: loading spinner on cancel button during API call.
 */
@Component({
  selector: 'app-appointment-history',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './appointment-history.component.html',
  styleUrl: './appointment-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentHistoryComponent implements OnInit, OnDestroy {
  private readonly appointmentApi = inject(AppointmentApiService);
  private readonly historyApi = inject(AppointmentHistoryApiService);
  private readonly bookingApi = inject(BookingApiService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly tokenStorage = inject(TokenStorageService);

  // ── Debounce pipeline ─────────────────────────────────────────────────────
  /** Emits whenever a filter or page changes; triggers the debounced API call. */
  private readonly filterChange$ = new Subject<void>();
  private readonly destroy$ = new Subject<void>();

  // ── State signals ─────────────────────────────────────────────────────────
  readonly appointments = signal<AppointmentHistoryItem[]>([]);
  readonly totalCount = signal(0);
  readonly isLoading = signal(true);
  readonly isExporting = signal(false);
  /** ID of the appointment currently being cancelled; null when idle. */
  readonly actionInProgress = signal<string | null>(null);
  /** ID of the appointment whose ICS is currently downloading; null when idle. */
  readonly downloadingId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  /** Controls visibility of the collapsible filter panel (toggled by topbar button). */
  readonly showFilters = signal(false);

  // ── Filter state (plain properties for ngModel two-way binding) ───────────
  statusFilter = '';
  dateFrom = '';
  dateTo = '';
  currentPage = 0;   // mat-paginator is 0-indexed; API is 1-indexed
  pageSize = 20;     // Edge case: 20 records per page

  /** Available status values for the filter select (AC-2). */
  readonly statuses = APPOINTMENT_STATUSES;

  /** True when the current JWT role is Staff or Admin (AC-4). */
  readonly isStaff = computed(() => {
    const role = this.tokenStorage.getUserRole();
    return role === 'Staff' || role === 'Admin';
  });

  /** Columns rendered in the Material table (desktop). Matches SCR-007 wireframe: Date | Provider | Visit type | Status | Actions. */
  readonly displayedColumns = [
    'scheduledAt',
    'providerName',
    'appointmentType',
    'status',
    'actions',
  ];

  ngOnInit(): void {
    // AC-2: 300 ms debounce + switchMap cancels in-flight requests on rapid changes.
    this.filterChange$
      .pipe(
        debounceTime(300),
        tap(() => this.isLoading.set(true)),
        switchMap(() => this.historyApi.getHistory(this.buildFilter())),
        takeUntil(this.destroy$),
      )
      .subscribe({
        next: (response) => {
          this.appointments.set(response.items);
          this.totalCount.set(response.totalCount);
          this.isLoading.set(false);
          this.errorMessage.set(null);
        },
        error: (err: HttpErrorResponse) => {
          this.isLoading.set(false);
          this.errorMessage.set(
            err.error?.message ?? 'Failed to load appointments. Please try again.',
          );
        },
      });

    // Trigger initial load.
    this.filterChange$.next();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.filterChange$.next();
  }

  /** AC-2 / AC-3: Called on any filter change; resets to page 1. */
  onFilterChange(): void {
    this.currentPage = 0;
    this.filterChange$.next();
  }

  retryLoad(): void {
    this.filterChange$.next();
  }

  clearFilters(): void {
    this.statusFilter = '';
    this.dateFrom = '';
    this.dateTo = '';
    this.currentPage = 0;
    this.filterChange$.next();
  }

  /** Toggles the filter panel open/closed (triggered by topbar ghost button). */
  toggleFilters(): void {
    this.showFilters.update((v) => !v);
  }

  /** Returns true when any filter is actively applied — used for the filter-badge dot. */
  hasActiveFilters(): boolean {
    return !!(this.statusFilter || this.dateFrom || this.dateTo);
  }

  /**
   * AC-4: Calls the export endpoint and triggers a browser blob download.
   * The PDF contains all filtered records regardless of current page.
   */
  exportPdf(): void {
    if (this.isExporting()) return;
    this.isExporting.set(true);

    this.historyApi.exportPdf(this.buildFilter()).subscribe({
      next: (blob) => {
        this.isExporting.set(false);

        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `appointment-history-${new Date().toISOString().split('T')[0]}.pdf`;
        anchor.click();
        URL.revokeObjectURL(url);

        this.snackBar.open('PDF exported successfully.', 'Close', {
          duration: 3_000,
        });
      },
      error: () => {
        this.isExporting.set(false);
        this.snackBar.open('Failed to export PDF. Please try again.', 'Close', {
          duration: 5_000,
        });
      },
    });
  }

  /**
   * Returns true when the user may act on this appointment.
   * Staff may always act (AC-4). Patients may only act when > 24 h away (AC-3).
   */
  canModify(apt: AppointmentHistoryItem): boolean {
    if (apt.status !== 'Confirmed') return false;
    if (this.isStaff()) return true;
    return this.isMoreThan24HoursAway(apt);
  }

  isMoreThan24HoursAway(apt: AppointmentHistoryItem): boolean {
    const appointmentMs = new Date(apt.scheduledAt).getTime();
    const twentyFourHoursMs = 24 * 60 * 60 * 1000;
    return appointmentMs - Date.now() > twentyFourHoursMs;
  }

  getActionTooltip(apt: AppointmentHistoryItem): string {
    if (apt.status !== 'Confirmed') {
      return 'Only confirmed appointments can be modified';
    }
    if (!this.isMoreThan24HoursAway(apt) && !this.isStaff()) {
      return 'Changes not allowed within 24 hours of appointment';
    }
    return '';
  }

  /**
   * AC-1 / AC-4: Opens the UXR-111 confirmation dialog then, on confirmation,
   * calls the cancel API and updates the row in-place.
   */
  cancelAppointment(apt: AppointmentHistoryItem): void {
    if (this.actionInProgress() !== null) return;

    const isWithin24h = !this.isMoreThan24HoursAway(apt);

    const dialogData: CancelDialogData = {
      appointmentTime: apt.scheduledAt,
      appointmentType: apt.appointmentType,
      isWithin24Hours: isWithin24h,
      isStaff: this.isStaff(),
    };

    const dialogRef = this.dialog.open(CancelDialogComponent, {
      data: dialogData,
      width: '480px',
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result: CancelDialogResult | undefined) => {
      if (!result?.confirmed) return;

      this.actionInProgress.set(apt.id);

      this.appointmentApi
        .cancelAppointment(apt.id, {
          overrideReason: result.overrideReason,
        })
        .subscribe({
          next: () => {
            this.actionInProgress.set(null);
            this.snackBar.open('Appointment cancelled successfully.', 'Close', {
              duration: 4000,
            });
            // Update the row status in-place (AC-1)
            this.appointments.update((list) =>
              list.map((a) =>
                a.id === apt.id ? { ...a, status: 'Cancelled' } : a,
              ),
            );
          },
          error: (err: HttpErrorResponse) => {
            this.actionInProgress.set(null);
            const message = err.error?.message ?? 'Failed to cancel appointment.';
            this.snackBar.open(message, 'Close', { duration: 5000 });
          },
        });
    });
  }

  rescheduleAppointment(apt: AppointmentHistoryItem): void {
    this.router.navigate(['/scheduling/search'], {
      queryParams: {
        rescheduleFrom: apt.id,
        appointmentType: apt.appointmentType,
        duration: apt.durationMinutes,
      },
    });
  }

  completeIntake(apt: AppointmentHistoryItem): void {
    this.router.navigate(['/scheduling/intake'], {
      queryParams: { appointmentId: apt.id },
    });
  }

  viewDetails(apt: AppointmentHistoryItem): void {
    this.dialog.open(AppointmentDetailDialogComponent, {
      data: apt,
      width: '480px',
    });
  }

  /** Opens Google Calendar in a new tab with pre-filled event details. */
  addToGoogleCalendar(apt: AppointmentHistoryItem): void {
    const start = new Date(apt.scheduledAt);
    const end = new Date(start.getTime() + apt.durationMinutes * 60_000);

    const formatGCalDate = (d: Date): string =>
      d.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');

    const params = new URLSearchParams({
      action: 'TEMPLATE',
      text: `${apt.appointmentType} – ${apt.providerName ?? 'Provider TBD'}`,
      dates: `${formatGCalDate(start)}/${formatGCalDate(end)}`,
      details: `Confirmation code: ${apt.confirmationCode}\nProvider: ${apt.providerName ?? 'TBD'}\nDuration: ${apt.durationMinutes} min`,
      ...(apt.location ? { location: apt.location } : {}),
    });

    window.open(
      `https://calendar.google.com/calendar/render?${params.toString()}`,
      '_blank',
      'noopener,noreferrer',
    );
  }

  downloadCalendarEvent(apt: AppointmentHistoryItem): void {
    if (this.downloadingId() !== null) return;
    this.downloadingId.set(apt.id);

    this.bookingApi.downloadArtifact(apt.id, 'ics').subscribe({
      next: (blob) => {
        this.downloadingId.set(null);

        // Trigger browser file download — blob URL is revoked after use.
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `appointment-${apt.confirmationCode}.ics`;
        anchor.click();
        URL.revokeObjectURL(url);

        this.snackBar.open('Calendar event downloaded.', 'Close', {
          duration: 3_000,
        });
      },
      error: () => {
        this.downloadingId.set(null);
        this.snackBar.open(
          'Failed to download calendar event. Please try again.',
          'Close',
          { duration: 5_000 },
        );
      },
    });
  }

  canDownloadIcs(apt: AppointmentHistoryItem): boolean {
    return apt.status === 'Confirmed' || apt.status === 'Rescheduled';
  }

  // ── Private helpers ───────────────────────────────────────────────────────

  private buildFilter(): AppointmentHistoryFilter {
    return {
      status: this.statusFilter || undefined,
      dateFrom: this.dateFrom || undefined,
      dateTo: this.dateTo || undefined,
      page: this.currentPage + 1, // API is 1-indexed
      pageSize: this.pageSize,
    };
  }
}
