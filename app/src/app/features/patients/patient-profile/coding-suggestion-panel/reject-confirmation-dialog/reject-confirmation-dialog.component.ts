import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { A11yModule } from '@angular/cdk/a11y';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';

/**
 * Destructive-action confirmation dialog for rejecting a coding suggestion (US_051 / AC-3).
 *
 * Accessibility requirements:
 * - `cdkTrapFocus` on dialog container — focus stays within dialog while open (UXR-206).
 * - Destructive "Reject" button uses `color="warn"` (`mat-flat-button`) (UXR-111).
 * - On close, the card component returns focus to the reject button that opened the dialog (UXR-206).
 * - `disableClose: true` is set by the caller via `MatDialog.open()` options.
 *
 * Caller pattern:
 * ```ts
 * const ref = this.dialog.open(RejectConfirmationDialogComponent, {
 *   disableClose: true,
 *   autoFocus: 'first-tabbable',
 *   restoreFocus: true,   // returns focus to trigger on close
 * });
 * ref.afterClosed().subscribe((confirmed: boolean) => {
 *   if (confirmed) { this.decisionFacade.reject(decisionId); }
 * });
 * ```
 */
@Component({
  selector: 'app-reject-confirmation-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [A11yModule, MatButtonModule, MatDialogModule],
  template: `
    <div cdkTrapFocus cdkTrapFocusAutoCapture role="dialog" aria-modal="true"
         aria-labelledby="reject-dialog-title" aria-describedby="reject-dialog-body"
         class="reject-dialog">

      <h2 mat-dialog-title id="reject-dialog-title">Reject Coding Suggestion</h2>

      <mat-dialog-content id="reject-dialog-body">
        <p>
          Are you sure you want to reject this coding suggestion?
          You will need to manually enter a code via code search.
        </p>
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button
          mat-stroked-button
          type="button"
          (click)="close(false)"
          aria-label="Cancel — keep suggestion"
        >
          Cancel
        </button>

        <button
          mat-flat-button
          type="button"
          color="warn"
          (click)="close(true)"
          aria-label="Confirm rejection of this coding suggestion"
        >
          Reject
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
    .reject-dialog {
      min-width: 320px;
      max-width: 480px;
    }

    mat-dialog-content p {
      font-size: 14px;
      line-height: 1.6;
      color: var(--color-neutral-800, #424242);
      margin: 0;
    }

    mat-dialog-actions {
      gap: 8px;
      padding-bottom: 8px;
    }
  `],
})
export class RejectConfirmationDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<RejectConfirmationDialogComponent>);

  protected close(confirmed: boolean): void {
    this.dialogRef.close(confirmed);
  }
}
