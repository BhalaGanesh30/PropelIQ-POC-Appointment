import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { JsonPipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

/** Data injected into the dialog by ConfigCategoryComponent on a 409 response. */
export interface ConflictDialogData {
  /** The values the current admin was about to save. */
  yourValues: Record<string, unknown>;
  /** The values that are currently saved on the server. */
  currentValues: Record<string, unknown>;
  /** Display name of the admin whose change caused the conflict. */
  updatedBy: string;
}

/**
 * Optimistic concurrency conflict resolution dialog (US_059, edge case 1).
 *
 * Shown when a PUT returns HTTP 409 Conflict — meaning another admin saved a
 * newer version while the current admin had the form open.
 *
 * Cancel: adopts server values (discards local changes).
 * Overwrite: retries the PUT with the refreshed ETag, sending the admin's values.
 */
@Component({
  selector: 'app-conflict-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [JsonPipe, MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Configuration Conflict Detected</h2>

    <mat-dialog-content>
      <p class="conflict-msg">
        <strong>{{ data.updatedBy }}</strong> updated this configuration after you loaded it.
        Your changes were not saved.
      </p>

      <div class="conflict-columns">
        <div class="conflict-col">
          <strong class="col-label">Your changes</strong>
          <pre class="conflict-json">{{ data.yourValues | json }}</pre>
        </div>
        <div class="conflict-col">
          <strong class="col-label">Current server value</strong>
          <pre class="conflict-json">{{ data.currentValues | json }}</pre>
        </div>
      </div>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <!-- Close(false): caller resets form to server values -->
      <button mat-button (click)="dialogRef.close(false)">
        Cancel — use server value
      </button>
      <!-- Close(true): caller retries PUT with refreshed ETag and current form values -->
      <button mat-raised-button color="primary" (click)="dialogRef.close(true)">
        Overwrite with my changes
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .conflict-msg    { margin-bottom: 16px; }
    .conflict-columns {
      display: flex;
      gap: 16px;
    }
    .conflict-col    { flex: 1; min-width: 0; }
    .col-label       { display: block; margin-bottom: 4px; font-size: 13px; color: #616161; }
    .conflict-json   {
      background: #f5f5f5;
      border-radius: 4px;
      padding: 8px;
      font-size: 12px;
      overflow: auto;
      max-height: 160px;
      white-space: pre-wrap;
      word-break: break-word;
    }
  `],
})
export class ConflictDialogComponent {
  readonly data      = inject<ConflictDialogData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<ConflictDialogComponent>);
}
