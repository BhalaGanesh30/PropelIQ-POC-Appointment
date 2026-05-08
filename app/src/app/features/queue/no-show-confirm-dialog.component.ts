import { ChangeDetectionStrategy, Component, Inject } from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

export interface NoShowConfirmDialogData {
  patientName: string;
}

/**
 * Confirmation dialog for No-Show state transition (US_032 AC-4, UXR-111).
 * Informs the staff member that the action is recorded in the audit log.
 * Returns `true` when confirmed, `undefined` on cancel.
 */
@Component({
  selector: 'app-no-show-confirm-dialog',
  standalone: true,
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Mark as No-Show?</h2>

    <mat-dialog-content>
      <p>
        Mark <strong>{{ data.patientName }}</strong> as No-Show?
      </p>
      <p class="audit-note">
        This action will be recorded in the audit log with your staff credentials.
      </p>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button color="warn" [mat-dialog-close]="true">
        Confirm No-Show
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .audit-note {
      color: rgba(0, 0, 0, 0.54);
      font-size: 0.875rem;
      margin-top: 8px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NoShowConfirmDialogComponent {
  constructor(
    public readonly dialogRef: MatDialogRef<NoShowConfirmDialogComponent, boolean>,
    @Inject(MAT_DIALOG_DATA) public readonly data: NoShowConfirmDialogData,
  ) {}
}
