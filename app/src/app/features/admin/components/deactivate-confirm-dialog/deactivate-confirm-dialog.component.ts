import {
  ChangeDetectionStrategy,
  Component,
  Inject,
} from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

export interface DeactivateDialogData {
  userName: string;
  userId: string;
}

/**
 * Confirmation dialog for staff account deactivation (AC-3, UXR-111).
 * Warns the Admin that all active sessions will be terminated.
 * Returns `true` when the Admin confirms, closes without value on cancel.
 */
@Component({
  selector: 'app-deactivate-confirm-dialog',
  standalone: true,
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Confirm Deactivation</h2>

    <mat-dialog-content>
      <p>
        Are you sure you want to deactivate
        <strong>{{ data.userName }}</strong>?
      </p>
      <p class="warning-text">
        All active sessions will be terminated immediately.
        The account can be reactivated by resending an invitation.
      </p>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button color="warn" [mat-dialog-close]="true">
        Deactivate
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .warning-text {
      color: rgba(0, 0, 0, 0.54);
      font-size: 0.875rem;
      margin-top: 8px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeactivateConfirmDialogComponent {
  constructor(
    public readonly dialogRef: MatDialogRef<DeactivateConfirmDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public readonly data: DeactivateDialogData,
  ) {}
}
