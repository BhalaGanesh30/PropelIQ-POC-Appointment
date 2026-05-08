import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  inject,
  signal,
} from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { QueueEntry } from './models/queue-entry.model';
import { AppointmentStateService } from './appointment-state.service';
import { NoShowConfirmDialogComponent } from './no-show-confirm-dialog.component';

/**
 * Row-level state-transition action buttons (EP-004 US_032).
 *
 * Renders contextual buttons based on the appointment's current QueueState:
 *   Scheduled  → "Check In"            (AC-1)
 *   Waiting    → "Start Visit"         (backward-compat for pre-US_032 rows)
 *   Arrived    → "Start Visit"         (AC-2)
 *   InProgress → "Complete Visit" + "No-Show" (AC-3, AC-4)
 *
 * UXR-501: Loading spinner + disabled state during in-flight PATCH request.
 * UXR-111: No-Show triggers a confirmation dialog before committing.
 * UXR-201: Success toast auto-dismisses at 4 s; error toast persists.
 *
 * Edge Case 1: Out-of-order transitions are rejected by the server state machine;
 *              the error snackbar surfaces the server-supplied message.
 */
@Component({
  selector: 'app-checkin-actions',
  standalone: true,
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './checkin-actions.component.html',
  styleUrl: './checkin-actions.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckinActionsComponent implements OnDestroy {
  @Input({ required: true }) entry!: QueueEntry;
  @Output() stateChanged = new EventEmitter<QueueEntry>();

  private readonly stateService = inject(AppointmentStateService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly destroy$ = new Subject<void>();

  /** UXR-501: Per-instance loading flag — disables all buttons during the request. */
  readonly loading = signal(false);

  // ── Action handlers ────────────────────────────────────────────────────────

  /** AC-1: Scheduled → Arrived. */
  checkIn(): void {
    this.dispatch('check-in');
  }

  /** AC-2: Arrived (or legacy Waiting) → InProgress. */
  startVisit(): void {
    this.dispatch('start-visit');
  }

  /** AC-3: InProgress → Completed. */
  completeVisit(): void {
    this.dispatch('complete-visit');
  }

  /**
   * AC-4: InProgress → NoShow.
   * UXR-111: Opens a confirmation dialog before submitting.
   */
  requestNoShow(): void {
    const ref = this.dialog.open(NoShowConfirmDialogComponent, {
      width: '400px',
      data: { patientName: this.entry.patientName },
    });

    ref
      .afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((confirmed: boolean | undefined) => {
        if (confirmed) {
          this.dispatch('no-show');
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Private ────────────────────────────────────────────────────────────────

  private dispatch(action: Parameters<AppointmentStateService['transitionState']>[1]): void {
    if (this.loading()) return; // guard against double-click

    this.loading.set(true);

    this.stateService
      .transitionState(this.entry.appointmentId, action)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (updated) => {
          this.loading.set(false);
          this.stateChanged.emit(updated);
          this.snackBar.open(
            `Status updated to ${updated.status.replace(/([A-Z])/g, ' $1').trim()}`,
            'Close',
            { duration: 4000 },
          );
        },
        error: (err) => {
          this.loading.set(false);
          // Edge Case 1: surface server-supplied error message or a generic fallback.
          const message: string =
            err?.error?.message ?? 'Failed to update status. Please try again.';
          this.snackBar.open(message, 'Dismiss', { duration: 0 });
        },
      });
  }
}
