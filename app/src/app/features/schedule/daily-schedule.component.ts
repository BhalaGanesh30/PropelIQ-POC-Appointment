import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CdkDragEnd, DragDropModule } from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, finalize } from 'rxjs';

import { DailyScheduleService } from './daily-schedule.service';
import { ScheduleAppointment } from '../../shared/models/schedule-appointment.model';
import { RescheduleRequest } from '../../shared/models/reschedule-request.model';
import {
  OverrideReasonDialogComponent,
  OverrideReasonDialogData,
  OverrideReasonDialogResult,
} from '../scheduling/override-reason-dialog.component';

/** Pixels per minute in the time-grid (4 px × 60 min = 240 px/hour). */
const PX_PER_MIN = 4;

/** Grid start in minutes from midnight (7:00 AM = 420). */
const GRID_START_MINUTES = 7 * 60;

/** Grid end in minutes from midnight (7:00 PM = 1140). */
const GRID_END_MINUTES = 19 * 60;

/** Snap granularity for drag calculation (15-minute intervals). */
const SNAP_MINUTES = 15;

interface TimeSlot {
  readonly minutes: number;
  readonly label: string;
  readonly isHour: boolean;
}

function formatTimeLabel(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  const period = h < 12 ? 'AM' : 'PM';
  const displayH = h % 12 === 0 ? 12 : h % 12;
  return `${displayH}:${m.toString().padStart(2, '0')} ${period}`;
}

function minutesFromIso(isoDateTime: string): number {
  const d = new Date(isoDateTime);
  return d.getHours() * 60 + d.getMinutes();
}

function addMinutesToIso(isoDateTime: string, delta: number): string {
  const d = new Date(isoDateTime);
  d.setMinutes(d.getMinutes() + delta);
  // Preserve yyyy-MM-ddTHH:mm:ss without timezone suffix so the API receives local time.
  return (
    d.getFullYear() +
    '-' +
    String(d.getMonth() + 1).padStart(2, '0') +
    '-' +
    String(d.getDate()).padStart(2, '0') +
    'T' +
    String(d.getHours()).padStart(2, '0') +
    ':' +
    String(d.getMinutes()).padStart(2, '0') +
    ':00'
  );
}

function dateToYmd(d: Date): string {
  return (
    d.getFullYear() +
    '-' +
    String(d.getMonth() + 1).padStart(2, '0') +
    '-' +
    String(d.getDate()).padStart(2, '0')
  );
}

/**
 * Daily Schedule Calendar (EP-004 US_036 SCR-026).
 *
 * AC-1: All appointments for the selected date are displayed in a time-grid
 *       layout (7 AM – 7 PM, 15-min intervals) sorted by appointment time.
 * AC-2: Dragging an appointment block to a free slot opens the Override Reason
 *       dialog (US_034); on confirmation the reschedule API is called and the
 *       queue is updated immediately.
 * AC-3: Print button triggers window.print(); @media print stylesheet renders
 *       an A4/Letter-formatted layout with all appointment details.
 * AC-4: Date picker loads appointments within 1 s (Redis-cached API backing).
 *
 * Edge Case 1: Conflict detected on drop → conflict banner shown, drop cancelled.
 * Edge Case 2: No appointments for date → empty-state message on the grid.
 *
 * UXR-110: Ghost block previews the appointment at the cursor during drag.
 * UXR-201: WCAG AA contrast on all type/bg pairs.
 * UXR-202: Full keyboard navigation; visible focus indicators.
 * UXR-301: Skeleton loader during data fetch (AC-4).
 * UXR-304: Error banner with retry on load failure.
 */
@Component({
  selector: 'app-daily-schedule',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DragDropModule,
    MatButtonModule,
    MatNativeDateModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './daily-schedule.component.html',
  styleUrl: './daily-schedule.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DailyScheduleComponent implements OnInit {
  private readonly scheduleService = inject(DailyScheduleService);
  private readonly dialog          = inject(MatDialog);
  private readonly snackBar        = inject(MatSnackBar);
  private readonly destroyRef      = inject(DestroyRef);

  /** Date picker control — initialised to today (AC-4). */
  protected readonly dateControl = new FormControl<Date>(new Date());

  /** Loaded appointments for the current date. */
  protected readonly appointments = signal<ScheduleAppointment[]>([]);

  /** True while the GET request is in flight (shows skeleton, UXR-301). */
  protected readonly isLoading = signal(false);

  /** Non-null string when the GET request failed (shows error banner, UXR-304). */
  protected readonly loadError = signal<string | null>(null);

  /** Set when a drag-drop results in a conflict (Edge Case 1). */
  protected readonly conflictMessage = signal<string | null>(null);

  /** True while the reschedule PUT is in flight (shows overlay spinner). */
  protected readonly isRescheduling = signal(false);

  /** Edge Case 2: grid is loaded, no error, but appointments array is empty. */
  protected readonly isEmpty = computed(
    () =>
      !this.isLoading() &&
      this.loadError() === null &&
      this.appointments().length === 0,
  );

  /** Total height of the time-grid body in pixels. */
  protected readonly gridHeight =
    (GRID_END_MINUTES - GRID_START_MINUTES) * PX_PER_MIN;

  /**
   * 48 rows representing 15-minute intervals from 7:00 AM to 6:45 PM.
   * Rendered as the time-label column and horizontal divider lines.
   */
  protected readonly timeSlots: readonly TimeSlot[] = (() => {
    const slots: TimeSlot[] = [];
    for (let m = GRID_START_MINUTES; m < GRID_END_MINUTES; m += SNAP_MINUTES) {
      slots.push({
        minutes: m,
        label: formatTimeLabel(m),
        isHour: m % 60 === 0,
      });
    }
    return slots;
  })();

  ngOnInit(): void {
    this.loadSchedule(dateToYmd(new Date()));

    this.dateControl.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((date) => {
        if (date) {
          this.loadSchedule(dateToYmd(date));
        }
      });
  }

  // ── Time-grid position helpers ─────────────────────────────────────────

  /** Top offset in pixels from the grid top edge for a given appointment. */
  protected topPx(appt: ScheduleAppointment): number {
    return (minutesFromIso(appt.startTime) - GRID_START_MINUTES) * PX_PER_MIN;
  }

  /** Height in pixels of an appointment block (minimum 20 px for legibility). */
  protected heightPx(appt: ScheduleAppointment): number {
    return Math.max(appt.duration * PX_PER_MIN, 20);
  }

  /** Returns the CSS class that colour-codes an appointment block by type. */
  protected typeClass(appt: ScheduleAppointment): string {
    const map: Record<string, string> = {
      Scheduled: 'type-scheduled',
      WalkIn:    'type-walkin',
      Override:  'type-override',
    };
    return map[appt.appointmentType] ?? 'type-scheduled';
  }

  /** Human-readable start time for an appointment (e.g. "9:30 AM"). */
  protected startLabel(appt: ScheduleAppointment): string {
    return formatTimeLabel(minutesFromIso(appt.startTime));
  }

  // ── Drag-and-drop ──────────────────────────────────────────────────────

  /**
   * Fired when the user releases a drag operation (UXR-110).
   *
   * 1. Resets the CDK transform so position is controlled by `[style.top]`.
   * 2. Snaps the drag distance to 15-min increments.
   * 3. Checks grid bounds and conflicts (Edge Case 1).
   * 4. If clear, opens the Override Reason dialog (US_034 AC-2).
   */
  protected onDragEnded(event: CdkDragEnd, appt: ScheduleAppointment): void {
    // Always reset the CDK translate transform — position is driven by data.
    event.source._dragRef.reset();

    const deltaY       = event.distance.y;
    const deltaMinutes =
      Math.round(deltaY / PX_PER_MIN / SNAP_MINUTES) * SNAP_MINUTES;

    if (deltaMinutes === 0) return;

    const newStartIso     = addMinutesToIso(appt.startTime, deltaMinutes);
    const newStartMinutes = minutesFromIso(newStartIso);

    // Clamp to visible grid bounds.
    if (
      newStartMinutes < GRID_START_MINUTES ||
      newStartMinutes + appt.duration > GRID_END_MINUTES
    ) {
      this.conflictMessage.set(
        'Cannot schedule outside the 7 AM – 7 PM window.',
      );
      return;
    }

    // Edge Case 1: overlap check against all other confirmed appointments.
    const conflict = this.appointments().find((other) => {
      if (other.appointmentId === appt.appointmentId) return false;
      const oStart = minutesFromIso(other.startTime);
      const oEnd   = oStart + other.duration;
      const nEnd   = newStartMinutes + appt.duration;
      return newStartMinutes < oEnd && nEnd > oStart;
    });

    if (conflict) {
      this.conflictMessage.set(
        `Time conflict with ${conflict.patientName} (${formatTimeLabel(minutesFromIso(conflict.startTime))}).` +
          ' Drop cancelled.',
      );
      return;
    }

    this.conflictMessage.set(null);
    this.openOverrideDialog(appt, newStartIso);
  }

  /** Dismiss the conflict banner. */
  protected dismissConflict(): void {
    this.conflictMessage.set(null);
  }

  // ── Print (AC-3) ───────────────────────────────────────────────────────

  protected printSchedule(): void {
    window.print();
  }

  // ── Load retry ────────────────────────────────────────────────────────

  protected retryLoad(): void {
    const date = this.dateControl.value;
    if (date) {
      this.loadSchedule(dateToYmd(date));
    }
  }

  // ── Private ───────────────────────────────────────────────────────────

  private loadSchedule(date: string): void {
    this.isLoading.set(true);
    this.loadError.set(null);
    this.conflictMessage.set(null);

    this.scheduleService
      .getSchedule(date)
      .pipe(
        catchError((err: unknown) => {
          const msg =
            err instanceof Error ? err.message : 'Failed to load schedule.';
          this.loadError.set(msg);
          return EMPTY;
        }),
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((data) => this.appointments.set(data));
  }

  /**
   * Opens the OverrideReasonDialogComponent (US_034) to capture a mandatory
   * reschedule reason, then calls the reschedule endpoint on confirmation (AC-2).
   */
  private openOverrideDialog(
    appt: ScheduleAppointment,
    newStartIso: string,
  ): void {
    const data: OverrideReasonDialogData = {
      appointmentId:         appt.appointmentId,
      constraintType:        'Reschedule',
      constraintDescription: `Rescheduling ${appt.patientName} to ${formatTimeLabel(minutesFromIso(newStartIso))}.`,
      action:                'Reschedule',
    };

    this.dialog
      .open<
        OverrideReasonDialogComponent,
        OverrideReasonDialogData,
        OverrideReasonDialogResult
      >(OverrideReasonDialogComponent, {
        data,
        width: '480px',
        disableClose: true,
      })
      .afterClosed()
      .subscribe((result) => {
        if (!result?.confirmed) return;

        const payload: RescheduleRequest = {
          appointmentId:  appt.appointmentId,
          newStartTime:   newStartIso,
          overrideReason: result.reason,
        };

        this.submitReschedule(appt, newStartIso, payload);
      });
  }

  private submitReschedule(
    appt: ScheduleAppointment,
    newStartIso: string,
    payload: RescheduleRequest,
  ): void {
    this.isRescheduling.set(true);

    this.scheduleService
      .reschedule(payload)
      .pipe(
        catchError((err: unknown) => {
          const msg =
            err instanceof Error
              ? err.message
              : 'Reschedule failed. Please try again.';
          this.snackBar.open(msg, 'Dismiss', {
            duration: 5000,
            panelClass: 'snack-error',
          });
          return EMPTY;
        }),
        finalize(() => this.isRescheduling.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        // Update the local signal so the block re-renders at the new position (AC-2).
        this.appointments.update((list) =>
          list.map((a) =>
            a.appointmentId === appt.appointmentId
              ? { ...a, startTime: newStartIso }
              : a,
          ),
        );

        this.snackBar.open('Appointment rescheduled.', undefined, {
          duration: 3000,
          panelClass: 'snack-success',
        });
      });
  }
}
