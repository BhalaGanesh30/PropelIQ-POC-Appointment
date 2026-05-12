import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface RestoreConfirmData {
  versionNumber: number;
}

/**
 * Confirmation dialog shown before restoring a previous template version (US_062, AC-3).
 *
 * Closes with `true` on confirm or `false`/undefined on cancel.
 * Informs the admin that queued notifications referencing the current version
 * are not affected (AC-3 requirement for user communication).
 *
 * Accessibility: title uses `mat-dialog-title`, body uses `mat-dialog-content` (UXR-205).
 */
@Component({
  selector: 'app-restore-confirm-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>Restore Version {{ data.versionNumber }}?</h2>

    <mat-dialog-content>
      <p>
        This will create a new active version containing the content from version
        <strong>{{ data.versionNumber }}</strong>.
      </p>
      <p class="hint">
        <mat-icon class="hint-icon" aria-hidden="true">info</mat-icon>
        Existing queued notifications that reference the current version will continue
        to use the content they were sent with — they will not be affected.
      </p>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close(false)">Cancel</button>
      <button mat-raised-button color="primary" (click)="dialogRef.close(true)">
        <mat-icon>restore</mat-icon>
        Restore
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .hint {
        display: flex;
        align-items: flex-start;
        gap: 8px;
        font-size: 13px;
        color: #555;
        margin-top: 12px;
        background: #f5f5f5;
        padding: 10px 12px;
        border-radius: 4px;
      }

      .hint-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
        flex-shrink: 0;
        margin-top: 1px;
        color: #1976d2;
      }
    `,
  ],
})
export class RestoreConfirmDialogComponent {
  readonly data = inject<RestoreConfirmData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<RestoreConfirmDialogComponent>);
}
