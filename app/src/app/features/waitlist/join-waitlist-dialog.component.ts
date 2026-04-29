import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatNativeDateModule } from '@angular/material/core';
import { JoinWaitlistRequest } from './models/waitlist.models';

/** Data injected into the join dialog for optional pre-fill from slot search filters. */
export interface JoinWaitlistDialogData {
  preferredDateStart?: Date | null;
  preferredDateEnd?: Date | null;
  preferredDurationMinutes?: number | null;
  preferredAppointmentType?: string | null;
}

/** Value emitted when the dialog closes (null = cancelled). */
export type JoinWaitlistDialogResult = JoinWaitlistRequest | null;

/** Available appointment-type choices (mirrors backend max-64 enum values). */
const APPOINTMENT_TYPES = [
  { value: 'General', label: 'General Consultation' },
  { value: 'FollowUp', label: 'Follow-up Visit' },
  { value: 'Specialist', label: 'Specialist Appointment' },
  { value: 'Procedure', label: 'Procedure / Treatment' },
  { value: 'Telehealth', label: 'Telehealth Video Visit' },
] as const;

/** Available duration options in minutes. */
const DURATION_OPTIONS = [
  { value: 15, label: '15 min' },
  { value: 30, label: '30 min' },
  { value: 60, label: '60 min' },
] as const;

/**
 * Dialog for joining the patient waitlist with preferred slot parameters (AC-1).
 *
 * Fields: preferred date range, appointment duration, appointment type.
 * Form uses plain string/number signals (not `[(ngModel)]` on WritableSignal)
 * to stay compatible with Angular OnPush CD.
 */
@Component({
  selector: 'app-join-waitlist-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatNativeDateModule,
    MatSelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      <mat-icon aria-hidden="true">playlist_add</mat-icon>
      Join Waitlist
    </h2>

    <mat-dialog-content>
      <p class="dialog-description">
        Enter your preferred slot criteria. You will be notified when a matching
        slot becomes available.
      </p>

      <!-- Preferred start date -->
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Preferred Start Date</mat-label>
        <input
          matInput
          [matDatepicker]="startPicker"
          placeholder="MM/DD/YYYY"
          [min]="today"
          [ngModel]="startDate()"
          (ngModelChange)="startDate.set($event)"
          name="startDate"
          required
          aria-required="true"
        />
        <mat-datepicker-toggle matSuffix [for]="startPicker" />
        <mat-datepicker #startPicker />
        @if (!startDate()) {
          <mat-error>Preferred start date is required.</mat-error>
        }
      </mat-form-field>

      <!-- Preferred end date -->
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Preferred End Date</mat-label>
        <input
          matInput
          [matDatepicker]="endPicker"
          placeholder="MM/DD/YYYY"
          [min]="startDate() ?? today"
          [ngModel]="endDate()"
          (ngModelChange)="endDate.set($event)"
          name="endDate"
          required
          aria-required="true"
        />
        <mat-datepicker-toggle matSuffix [for]="endPicker" />
        <mat-datepicker #endPicker />
        @if (!endDate()) {
          <mat-error>Preferred end date is required.</mat-error>
        }
        @if (endDate() && startDate() && endDate()! <= startDate()!) {
          <mat-error>End date must be after start date.</mat-error>
        }
      </mat-form-field>

      <!-- Duration -->
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Appointment Duration</mat-label>
        <mat-select
          [ngModel]="duration()"
          (ngModelChange)="duration.set($event)"
          name="duration"
          required
          aria-required="true"
        >
          @for (opt of durationOptions; track opt.value) {
            <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
          }
        </mat-select>
        @if (!duration()) {
          <mat-error>Duration is required.</mat-error>
        }
      </mat-form-field>

      <!-- Appointment type -->
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Appointment Type</mat-label>
        <mat-select
          [ngModel]="appointmentType()"
          (ngModelChange)="appointmentType.set($event)"
          name="appointmentType"
          required
          aria-required="true"
        >
          @for (opt of appointmentTypes; track opt.value) {
            <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
          }
        </mat-select>
        @if (!appointmentType()) {
          <mat-error>Appointment type is required.</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()" type="button">Cancel</button>
      <button
        mat-flat-button
        color="primary"
        [disabled]="!canSubmit()"
        (click)="submit()"
        type="button"
        aria-label="Confirm join waitlist"
      >
        Join Waitlist
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      mat-dialog-content {
        display: flex;
        flex-direction: column;
        gap: 16px;
        min-width: 320px;
        max-width: 480px;
      }

      .dialog-description {
        margin: 0 0 8px;
        color: var(--mat-sys-on-surface-variant, #5f5f5f);
        font-size: 14px;
      }

      .full-width {
        width: 100%;
      }
    `,
  ],
})
export class JoinWaitlistDialogComponent {
  private readonly dialogRef =
    inject<MatDialogRef<JoinWaitlistDialogComponent, JoinWaitlistDialogResult>>(
      MatDialogRef,
    );

  readonly data = inject<JoinWaitlistDialogData>(MAT_DIALOG_DATA);

  readonly today = new Date();

  readonly appointmentTypes = APPOINTMENT_TYPES;
  readonly durationOptions = DURATION_OPTIONS;

  readonly startDate = signal<Date | null>(this.data?.preferredDateStart ?? null);
  readonly endDate = signal<Date | null>(this.data?.preferredDateEnd ?? null);
  readonly duration = signal<15 | 30 | 60 | null>(
    (this.data?.preferredDurationMinutes as 15 | 30 | 60 | null) ?? null,
  );
  readonly appointmentType = signal<string>(this.data?.preferredAppointmentType ?? '');

  readonly canSubmit = computed(() => {
    const start = this.startDate();
    const end = this.endDate();
    return (
      start !== null &&
      end !== null &&
      end > start &&
      this.duration() !== null &&
      this.appointmentType().length > 0
    );
  });

  cancel(): void {
    this.dialogRef.close(null);
  }

  submit(): void {
    const start = this.startDate();
    const end = this.endDate();
    const dur = this.duration();
    if (!start || !end || !dur) return;

    const request: JoinWaitlistRequest = {
      preferredDateStart: start.toISOString(),
      preferredDateEnd: end.toISOString(),
      preferredDurationMinutes: dur,
      preferredAppointmentType: this.appointmentType(),
    };

    this.dialogRef.close(request);
  }
}
