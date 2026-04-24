import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

/** Data injected into CancelDialogComponent via MAT_DIALOG_DATA. */
export interface CancelDialogData {
  appointmentTime: string;
  appointmentType: string;
  /** True when the appointment is within the 24-hour window. */
  isWithin24Hours: boolean;
  /** True when the acting user holds a staff role. */
  isStaff: boolean;
}

/** Value emitted when the dialog closes. */
export interface CancelDialogResult {
  confirmed: boolean;
  overrideReason?: string;
}

/**
 * UXR-111 confirmation dialog for the destructive cancel action (SCR-007).
 *
 * When `isWithin24Hours && isStaff` (AC-4), a mandatory override-reason
 * textarea is rendered; the confirm button remains disabled until filled.
 */
@Component({
  selector: 'app-cancel-dialog',
  standalone: true,
  imports: [
    FormsModule,
    DatePipe,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon color="warn" aria-hidden="true">warning</mat-icon>
      Cancel Appointment
    </h2>

    <mat-dialog-content>
      <p>
        Are you sure you want to cancel your
        <strong>{{ data.appointmentType }}</strong> appointment on
        <strong>{{ data.appointmentTime | date : 'EEEE, MMM d, y h:mm a' }}</strong>?
      </p>
      <p class="warning-text">This action cannot be undone.</p>

      <!-- AC-4: staff override reason (required when within 24 h) -->
      @if (requiresReason()) {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Override Reason (Required)</mat-label>
          <textarea
            matInput
            [ngModel]="overrideReason()"
            (ngModelChange)="overrideReason.set($event)"
            rows="3"
            maxlength="1000"
            placeholder="Explain why this cancellation is needed within 24 hours"
            required
            aria-label="Override reason for cancellation within 24 hours"
          ></textarea>
          <mat-hint align="end">{{ overrideReason().length }} / 1000</mat-hint>
        </mat-form-field>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close aria-label="Keep appointment">
        Keep Appointment
      </button>
      <button
        mat-flat-button
        color="warn"
        [disabled]="!canConfirm()"
        (click)="confirm()"
        aria-label="Confirm cancellation"
      >
        <mat-icon aria-hidden="true">cancel</mat-icon>
        Confirm Cancellation
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .warning-text {
        color: var(--mat-sys-error);
        font-weight: 500;
        margin-top: 8px;
      }

      .full-width {
        width: 100%;
        margin-top: 16px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CancelDialogComponent {
  readonly data = inject<CancelDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<CancelDialogComponent>);

  /** Writable signal bound to the textarea via [(ngModel)]. */
  readonly overrideReason = signal('');

  /** True when the staff-override reason textarea must be filled (AC-4). */
  readonly requiresReason = computed(
    () => this.data.isWithin24Hours && this.data.isStaff,
  );

  /** Confirm button is enabled once mandatory conditions are satisfied. */
  readonly canConfirm = computed(() => {
    if (this.requiresReason()) {
      return this.overrideReason().trim().length > 0;
    }
    return true;
  });

  confirm(): void {
    if (!this.canConfirm()) return;

    const result: CancelDialogResult = {
      confirmed: true,
      overrideReason: this.requiresReason()
        ? this.overrideReason().trim()
        : undefined,
    };
    this.dialogRef.close(result);
  }
}
