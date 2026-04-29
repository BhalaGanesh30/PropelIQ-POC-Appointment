import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
} from '@angular/forms';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { SlotSearchService } from '../../services/slot-search.service';
import { BookingApiService } from '../../services/booking-api.service';
import {
  AppointmentType,
  SlotDto,
  SlotSearchResponse,
} from '../../models/slot.model';
import {
  JoinWaitlistDialogComponent,
  JoinWaitlistDialogData,
} from '../../../../features/waitlist/join-waitlist-dialog.component';
import { WaitlistApiService } from '../../../../features/waitlist/waitlist-api.service';

type SearchState = 'idle' | 'loading' | 'success' | 'empty' | 'error';

/**
 * Slot Search and Discovery page — implements SCR-004.
 *
 * States: idle (filter bar), loading (skeleton), success (grouped results),
 * empty (AC-3 waitlist CTA), error (retry banner), validation (sticky footer, UXR-503).
 *
 * UXR-103: date range picker with 30-day maximum window.
 * UXR-201: WCAG 2.1 AA contrast via Material theme tokens.
 * UXR-202: keyboard navigation on all controls and slot cards.
 * UXR-301: responsive at 375px / 768px / 1440px breakpoints.
 * UXR-303: grid results on desktop; vertical cards on mobile.
 * UXR-304: 44×44px minimum touch targets.
 * UXR-503: selected slot border emphasis + sticky footer.
 */
@Component({
  selector: 'app-slot-search',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatDatepickerModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatSnackBarModule,
  ],
  templateUrl: './slot-search.component.html',
  styleUrl: './slot-search.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SlotSearchComponent implements OnInit {
  private static readonly MAX_RANGE_DAYS = 30;
  private static readonly MS_PER_DAY = 86_400_000;

  private readonly slotSearchService = inject(SlotSearchService);
  private readonly bookingApi = inject(BookingApiService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly waitlistApi = inject(WaitlistApiService);

  readonly isBooking = signal(false);
  readonly searchState = signal<SearchState>('idle');
  readonly searchResult = signal<SlotSearchResponse | null>(null);
  readonly selectedSlot = signal<SlotDto | null>(null);
  readonly dateRangeError = signal<string | null>(null);

  readonly isLoading = computed(() => this.searchState() === 'loading');

  readonly minDate = new Date();
  readonly maxDate = new Date(
    Date.now() + SlotSearchComponent.MAX_RANGE_DAYS * SlotSearchComponent.MS_PER_DAY,
  );

  readonly durations: { value: 15 | 30 | 60; label: string }[] = [
    { value: 15, label: '15 minutes' },
    { value: 30, label: '30 minutes' },
    { value: 60, label: '60 minutes' },
  ];

  readonly appointmentTypes: { value: AppointmentType; label: string }[] = [
    { value: 'General', label: 'General' },
    { value: 'Specialist', label: 'Specialist' },
    { value: 'FollowUp', label: 'Follow-Up' },
    { value: 'Urgent', label: 'Urgent' },
  ];

  readonly filterForm = new FormGroup({
    dateFrom: new FormControl<Date | null>(null),
    dateTo: new FormControl<Date | null>(null),
    duration: new FormControl<15 | 30 | 60 | null>(null),
    type: new FormControl<AppointmentType | null>(null),
  });

  ngOnInit(): void {
    // Default: today → today + 7 days
    const today = new Date();
    const nextWeek = new Date(
      today.getTime() + 7 * SlotSearchComponent.MS_PER_DAY,
    );
    this.filterForm.patchValue({ dateFrom: today, dateTo: nextWeek });
  }

  onSearch(): void {
    const { dateFrom, dateTo, duration, type } = this.filterForm.value;

    if (!dateFrom || !dateTo) {
      return;
    }

    // Client-side 30-day window validation (AC-4)
    const diffDays = Math.ceil(
      (dateTo.getTime() - dateFrom.getTime()) / SlotSearchComponent.MS_PER_DAY,
    );
    if (diffDays > SlotSearchComponent.MAX_RANGE_DAYS) {
      this.dateRangeError.set('Slot search is limited to the next 30 days.');
      return;
    }
    this.dateRangeError.set(null);
    this.searchState.set('loading');
    this.selectedSlot.set(null);

    this.slotSearchService
      .searchSlots({
        dateFrom: this.toIsoDate(dateFrom),
        dateTo: this.toIsoDate(dateTo),
        duration: duration ?? undefined,
        type: type ?? undefined,
      })
      .subscribe({
        next: (result) => {
          this.searchResult.set(result);
          this.searchState.set(result.hasResults ? 'success' : 'empty');
        },
        error: (err: HttpErrorResponse) => {
          if (err.status === 400) {
            // Surface the API validation message for AC-4 (server-side catch)
            const apiMsg =
              err.error?.errors?.DateRange?.[0] ??
              err.error?.errors?.dateRange?.[0] ??
              'Invalid search parameters.';
            this.dateRangeError.set(apiMsg);
            this.searchState.set('idle');
          } else {
            this.searchState.set('error');
          }
        },
      });
  }

  onSlotSelected(slot: SlotDto): void {
    this.selectedSlot.set(slot);
  }

  onConfirmBooking(): void {
    const slot = this.selectedSlot();
    if (!slot || this.isBooking()) return;

    this.isBooking.set(true);

    this.bookingApi.createBooking({ slotId: slot.id }).subscribe({
      next: (booking) => {
        this.isBooking.set(false);
        this.router.navigate(
          ['/scheduling/booking/confirmation', booking.appointmentId],
        );
      },
      error: (err: HttpErrorResponse) => {
        this.isBooking.set(false);
        if (err.status === 409) {
          this.snackBar.open(
            'This slot was just taken. Please select another.',
            'Dismiss',
            { duration: 5000, panelClass: 'snack-error' },
          );
          this.selectedSlot.set(null);
          this.onSearch();
        } else {
          this.snackBar.open(
            err.error?.title ?? 'Booking failed. Please try again.',
            'Dismiss',
            { duration: 5000, panelClass: 'snack-error' },
          );
        }
      },
    });
  }

  onJoinWaitlist(): void {
    const { dateFrom, dateTo, duration, type } = this.filterForm.value;
    const selectedSlot = this.selectedSlot();

    const dialogData: JoinWaitlistDialogData = {
      preferredDateStart: selectedSlot
        ? new Date(selectedSlot.startTime)
        : (dateFrom ?? null),
      preferredDateEnd: selectedSlot
        ? new Date(selectedSlot.endTime)
        : (dateTo ?? null),
      preferredDurationMinutes: selectedSlot
        ? selectedSlot.durationMinutes
        : (duration ?? null),
      preferredAppointmentType: selectedSlot
        ? selectedSlot.type
        : (type ?? null),
    };

    const ref = this.dialog.open(JoinWaitlistDialogComponent, {
      data: dialogData,
      width: '500px',
      maxWidth: '95vw',
    });

    ref.afterClosed().subscribe((request) => {
      if (!request) return;

      this.waitlistApi.joinWaitlist(request).subscribe({
        next: () => {
          this.snackBar.open(
            'You have been added to the waitlist. We will notify you when a slot becomes available.',
            'View Waitlist',
            { duration: 6000, panelClass: 'snack-success' },
          ).onAction().subscribe(() => {
            this.router.navigate(['/waitlist']);
          });
          this.router.navigate(['/waitlist']);
        },
        error: (err: HttpErrorResponse) => {
          const message =
            err.status === 409
              ? 'You are already on the waitlist for a matching slot.'
              : 'Failed to join the waitlist. Please try again.';
          this.snackBar.open(message, 'Dismiss', {
            duration: 5000,
            panelClass: 'snack-error',
          });
        },
      });
    });
  }

  onRetry(): void {
    this.onSearch();
  }

  private toIsoDate(date: Date): string {
    return date.toISOString().split('T')[0];
  }
}
