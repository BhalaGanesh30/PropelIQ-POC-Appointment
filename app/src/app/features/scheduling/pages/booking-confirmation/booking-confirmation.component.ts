import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BookingApiService } from '../../services/booking-api.service';
import { SlotConflictDialogComponent } from '../../components/slot-conflict-dialog/slot-conflict-dialog.component';
import { ArtifactType, BookingResponse } from '../../models/booking.model';

/**
 * Booking Confirmation page — SCR-006.
 *
 * States:
 *   loading    — fetching booking by appointmentId route param
 *   submitting — POST /api/v1/bookings in-flight (UXR-501: spinner + double-submit lock)
 *   default    — confirmation card with success banner
 *   error      — inline error with retry
 *
 * AC-1: booking submitted; confirmation returned within 1 minute.
 * AC-2: PDF / QR / ICS available as downloads from the confirmation card.
 * AC-3: "Download PDF" triggers blob download immediately.
 * AC-4: HTTP 409 → SlotConflictDialog with next-slot suggestion.
 * UXR-105: PDF, QR, ICS buttons in a single action row.
 * UXR-301: responsive at 375 / 768 / 1440px.
 * UXR-501: spinner on submit; isSubmitting guard prevents double-click.
 */
@Component({
  selector: 'app-booking-confirmation',
  standalone: true,
  imports: [
    DatePipe,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './booking-confirmation.component.html',
  styleUrl: './booking-confirmation.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingConfirmationComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly bookingApi = inject(BookingApiService);
  private readonly dialog = inject(MatDialog);

  // ── State signals ──────────────────────────────────────────────────────────
  readonly booking = signal<BookingResponse | null>(null);
  readonly isLoading = signal(true);
  readonly isSubmitting = signal(false);
  readonly downloadingArtifact = signal<ArtifactType | null>(null);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    // If navigated here from intake submit with booking in router state, use it.
    const state = history.state as { booking?: BookingResponse };
    if (state?.booking) {
      this.booking.set(state.booking);
      this.isLoading.set(false);
      return;
    }

    const appointmentId = this.route.snapshot.paramMap.get('appointmentId');
    if (appointmentId) {
      this.loadBooking(appointmentId);
    } else {
      this.isLoading.set(false);
      this.errorMessage.set('No appointment ID provided.');
    }
  }

  /**
   * AC-1: Submit a new booking from the intake flow.
   * Called externally (e.g. intake form navigates here after passing slotId +
   * intakeRecordId) or used when booking is driven from this page's context.
   * UXR-501: isSubmitting guard prevents double-submit.
   */
  submitBooking(slotId: string, intakeRecordId: string): void {
    if (this.isSubmitting()) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.bookingApi.createBooking({ slotId, intakeRecordId }).subscribe({
      next: (response) => {
        this.booking.set(response);
        this.isSubmitting.set(false);
        this.isLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        if (err.status === 409) {
          this.handleSlotConflict(err.error);
        } else {
          this.errorMessage.set('Failed to create booking. Please try again.');
        }
      },
    });
  }

  /**
   * AC-3: Download a confirmation artifact (PDF / QR / ICS).
   * Disabled while any other download is in-flight.
   */
  downloadArtifact(type: ArtifactType): void {
    const id = this.booking()?.appointmentId;
    if (!id || this.downloadingArtifact() !== null) return;

    this.downloadingArtifact.set(type);

    const fileNames: Record<ArtifactType, string> = {
      pdf: 'confirmation.pdf',
      qr: 'qrcode.png',
      ics: 'appointment.ics',
    };

    this.bookingApi.downloadArtifact(id, type).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileNames[type];
        anchor.click();
        URL.revokeObjectURL(url);
        this.downloadingArtifact.set(null);
      },
      error: () => {
        this.downloadingArtifact.set(null);
        this.errorMessage.set('Artifact not yet available. Please try again shortly.');
      },
    });
  }

  clearError(): void {
    this.errorMessage.set(null);
  }

  navigateToAppointments(): void {
    this.router.navigate(['/appointments']);
  }

  navigateToIntake(): void {
    const id = this.booking()?.appointmentId;
    if (!id) return;
    this.router.navigate(['/scheduling/intake'], {
      queryParams: { appointmentId: id },
    });
  }

  private loadBooking(appointmentId: string): void {
    this.bookingApi.getBooking(appointmentId).subscribe({
      next: (response) => {
        this.booking.set(response);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Could not load booking details.');
      },
    });
  }

  /** AC-4: Open the conflict dialog; handle user choice. */
  private handleSlotConflict(conflict: unknown): void {
    const ref = this.dialog.open(SlotConflictDialogComponent, {
      data: conflict,
      width: '480px',
    });

    ref.afterClosed().subscribe((result: string | undefined) => {
      if (result === 'search') {
        this.router.navigate(['/scheduling/search']);
      } else if (result) {
        // Re-navigate to search with the suggested slot pre-selected.
        this.router.navigate(['/scheduling/search'], {
          queryParams: { slotId: result },
        });
      }
    });
  }
}
