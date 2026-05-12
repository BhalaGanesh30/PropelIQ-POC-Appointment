import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

/**
 * Dialog for configuring the KPI report distribution schedule (US_060, AC-4).
 *
 * Collects:
 * - `recurrence`: daily / weekly (Monday) / monthly (1st).
 * - `recipients`: comma-separated email addresses written to the
 *    `CommunicationTemplates.kpiDistributionRecipients` configuration key
 *    (read by `KpiDistributionWorker` in the backend).
 *
 * Returns `{ recurrence, recipients }` on save; `undefined` on cancel.
 */
@Component({
  selector: 'app-distribution-config-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Schedule KPI Report Distribution</h2>

    <mat-dialog-content>
      <form [formGroup]="form" novalidate>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Recurrence</mat-label>
          <mat-select formControlName="recurrence">
            <mat-option value="daily">Daily</mat-option>
            <mat-option value="weekly">Weekly (every Monday at 08:00 UTC)</mat-option>
            <mat-option value="monthly">Monthly (1st of the month)</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Recipient Email Addresses</mat-label>
          <input
            matInput
            formControlName="recipients"
            placeholder="admin@example.com, ops@example.com"
            aria-describedby="recipients-hint" />
          <mat-hint id="recipients-hint">Comma-separated email addresses</mat-hint>
          @if (form.get('recipients')?.invalid && form.get('recipients')?.touched) {
            <mat-error>Enter one or more valid email addresses, separated by commas.</mat-error>
          }
        </mat-form-field>

      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button
        mat-raised-button
        color="primary"
        [disabled]="form.invalid"
        (click)="save()">
        Save Schedule
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .full-width { width: 100%; }
      mat-dialog-content { padding-top: 8px; min-width: 380px; }
      form { display: flex; flex-direction: column; gap: 12px; }
    `,
  ],
})
export class DistributionConfigDialogComponent {
  private readonly dialogRef = inject(
    MatDialogRef<DistributionConfigDialogComponent>,
  );

  // Email list pattern: one or more comma-separated valid email addresses.
  private static readonly EMAIL_LIST_PATTERN =
    /^[\w.+\-]+@[\w\-]+\.[\w.]{2,}(\s*,\s*[\w.+\-]+@[\w\-]+\.[\w.]{2,})*$/;

  readonly form = new FormGroup({
    recurrence: new FormControl<string>('weekly', { nonNullable: true, validators: [Validators.required] }),
    recipients: new FormControl<string>('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.pattern(DistributionConfigDialogComponent.EMAIL_LIST_PATTERN),
      ],
    }),
  });

  save(): void {
    if (this.form.valid) {
      this.dialogRef.close(this.form.getRawValue());
    }
  }

  cancel(): void {
    this.dialogRef.close(undefined);
  }
}
