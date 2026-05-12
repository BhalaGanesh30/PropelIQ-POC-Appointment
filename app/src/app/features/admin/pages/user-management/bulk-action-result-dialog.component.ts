import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { LowerCasePipe } from '@angular/common';
import { BulkActionResult, BulkActionTypeName } from './models/user.models';

export interface BulkActionResultData {
  result: BulkActionResult;
  action: BulkActionTypeName;
}

@Component({
  selector: 'app-bulk-action-result-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, MatListModule, LowerCasePipe],
  template: `
    <h2 mat-dialog-title>Bulk Action Complete</h2>
    <mat-dialog-content>
      <p class="success-summary">
        <mat-icon class="inline-icon success">check_circle</mat-icon>
        <strong>{{ data.result.successCount }}</strong>
        user(s) {{ data.action | lowercase }}d successfully.
      </p>

      @if (data.result.failureCount > 0) {
        <p class="failure-summary">
          <mat-icon class="inline-icon warn">warning</mat-icon>
          <strong>{{ data.result.failureCount }}</strong> user(s) could not be updated:
        </p>
        <mat-list>
          @for (f of data.result.failures; track f.userId) {
            <mat-list-item>
              <mat-icon matListItemIcon color="warn">error_outline</mat-icon>
              <span matListItemTitle>{{ f.userName }}</span>
              <span matListItemLine>{{ f.reason }}</span>
            </mat-list-item>
          }
        </mat-list>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-raised-button color="primary" mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .success-summary, .failure-summary {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 8px;
      }
      .inline-icon { font-size: 20px; width: 20px; height: 20px; }
      .inline-icon.success { color: #2e7d32; }
      .inline-icon.warn { color: #ef6c00; }
    `,
  ],
})
export class BulkActionResultDialogComponent {
  readonly data = inject<BulkActionResultData>(MAT_DIALOG_DATA);
}
