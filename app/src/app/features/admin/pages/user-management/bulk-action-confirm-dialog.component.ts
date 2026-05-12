import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { FormsModule } from '@angular/forms';
import { BulkActionTypeName } from './models/user.models';

export interface BulkActionConfirmData {
  action: BulkActionTypeName;
  count: number;
}

export interface BulkActionConfirmResult {
  targetRole?: string;
}

@Component({
  selector: 'app-bulk-action-confirm-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatFormFieldModule, MatSelectModule, FormsModule],
  template: `
    <h2 mat-dialog-title>Confirm {{ data.action }}</h2>
    <mat-dialog-content>
      <p>
        Apply <strong>{{ data.action }}</strong> to
        <strong>{{ data.count }}</strong> selected user(s)?
      </p>
      @if (data.action === 'Deactivate') {
        <p class="warn-note">
          Note: the last remaining admin account cannot be deactivated.
        </p>
      }
      @if (data.action === 'AssignRole') {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Target Role</mat-label>
          <mat-select [(ngModel)]="targetRole" required>
            <mat-option value="Admin">Admin</mat-option>
            <mat-option value="Staff">Staff</mat-option>
            <mat-option value="Clinician">Clinician</mat-option>
            <mat-option value="Patient">Patient</mat-option>
          </mat-select>
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close(null)">Cancel</button>
      <button
        mat-raised-button
        [color]="data.action === 'Deactivate' ? 'warn' : 'primary'"
        [disabled]="data.action === 'AssignRole' && !targetRole"
        (click)="confirm()"
      >
        {{ data.action }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .full-width { width: 100%; margin-top: 8px; }
      .warn-note { color: #c62828; font-size: 0.875rem; margin-top: 8px; }
    `,
  ],
})
export class BulkActionConfirmDialogComponent {
  readonly data = inject<BulkActionConfirmData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<BulkActionConfirmDialogComponent>);

  targetRole = '';

  confirm(): void {
    const result: BulkActionConfirmResult = {
      targetRole: this.targetRole || undefined,
    };
    this.dialogRef.close(result);
  }
}
