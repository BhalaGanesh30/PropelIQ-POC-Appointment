import {
  ChangeDetectionStrategy,
  Component,
  signal,
  inject,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { StaffManagementService } from '../../services/staff-management.service';

/**
 * Modal dialog for sending a staff invitation (AC-1).
 * Validates name, email, and role before calling the invite API.
 * Shows a loading spinner during submission (UXR-501).
 * On success, closes with `true` so the parent can refresh the list.
 */
@Component({
  selector: 'app-invite-staff-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Invite Staff Member</h2>

    <mat-dialog-content>
      <form [formGroup]="form" class="invite-form" novalidate>
        @if (serverError()) {
          <div class="server-error" role="alert" aria-live="polite">
            {{ serverError() }}
          </div>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Full Name</mat-label>
          <input
            matInput
            formControlName="fullName"
            autocomplete="name"
          />
          @if (form.controls.fullName.touched && form.controls.fullName.hasError('required')) {
            <mat-error>Full name is required</mat-error>
          }
          @if (form.controls.fullName.touched && form.controls.fullName.hasError('maxlength')) {
            <mat-error>Maximum 200 characters</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email Address</mat-label>
          <input
            matInput
            formControlName="email"
            type="email"
            autocomplete="email"
          />
          @if (form.controls.email.touched && form.controls.email.hasError('required')) {
            <mat-error>Email is required</mat-error>
          }
          @if (form.controls.email.touched && form.controls.email.hasError('email')) {
            <mat-error>A valid email address is required</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">
            <mat-option value="Staff">Staff</mat-option>
            <mat-option value="Clinician">Clinician</mat-option>
            <mat-option value="Admin">Admin</mat-option>
          </mat-select>
          @if (form.controls.role.touched && form.controls.role.hasError('required')) {
            <mat-error>Role is required</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-raised-button
        color="primary"
        [disabled]="form.invalid || isSubmitting()"
        (click)="onSubmit()"
      >
        @if (isSubmitting()) {
          <mat-spinner diameter="20" />
        } @else {
          Send Invitation
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .invite-form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: 400px;
    }
    .full-width { width: 100%; }
    .server-error {
      padding: 8px 12px;
      background: #fdecea;
      border: 1px solid #f5c6cb;
      border-radius: 4px;
      color: #c62828;
      font-size: 0.875rem;
      margin-bottom: 8px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InviteStaffDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<InviteStaffDialogComponent>);
  private readonly staffService = inject(StaffManagementService);

  readonly form = new FormGroup({
    fullName: new FormControl('', [
      Validators.required,
      Validators.maxLength(200),
    ]),
    email: new FormControl('', [
      Validators.required,
      Validators.email,
    ]),
    role: new FormControl('Staff', [Validators.required]),
  });

  readonly isSubmitting = signal(false);
  readonly serverError = signal<string | null>(null);

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.serverError.set(null);

    const { fullName, email, role } = this.form.value;

    this.staffService.inviteStaff({
      fullName: fullName!,
      email: email!,
      role: role!,
    }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.dialogRef.close(true);
      },
      error: (err: { error?: { detail?: string } }) => {
        this.isSubmitting.set(false);
        this.serverError.set(err.error?.detail ?? 'Failed to send invitation.');
      },
    });
  }
}
