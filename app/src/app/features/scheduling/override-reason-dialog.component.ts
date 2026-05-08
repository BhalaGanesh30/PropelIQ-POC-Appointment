import {
  ChangeDetectionStrategy,
  Component,
  Inject,
  OnDestroy,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subject, takeUntil } from 'rxjs';

import { OverrideService } from './override.service';
import { OverrideRequest } from '../../shared/models/override-request.model';
import { OverrideResponse } from '../../shared/models/override-response.model';

/** Warning zone threshold — counter turns amber at this character count. */
const CHAR_WARNING_THRESHOLD = 480;

/** Maximum override reason length (enforced by server and client). */
const MAX_REASON_LENGTH = 500;

/** Data injected into the dialog via MAT_DIALOG_DATA. */
export interface OverrideReasonDialogData {
  /** UUID of the appointment being overridden. */
  appointmentId: string;

  /**
   * Machine-readable constraint type key (e.g., "SameDayWindow").
   * Sent to the server as part of the override payload.
   */
  constraintType: string;

  /**
   * Human-readable description of the violated constraint, shown as
   * read-only context above the textarea (AC-1).
   */
  constraintDescription: string;

  /**
   * The scheduling action being overridden (e.g., "Cancel", "Reschedule").
   * Sent to the server as part of the override payload.
   */
  action: string;
}

/** Value returned when the dialog closes via confirmation. */
export interface OverrideReasonDialogResult {
  confirmed: true;
  reason: string;
  overrideId: string;
  auditRecordId: string;
}

/**
 * Override reason dialog (EP-004 US_034 SCR-027).
 *
 * AC-1: Shows mandatory dialog when a scheduling constraint blocks an action.
 * AC-2: On confirmation, submits the override payload and returns the result.
 * AC-3: "Override reason is required" shown on empty/whitespace-only submit.
 * Edge Case 1: Character counter turns amber at 480+, error (red) at 500+.
 * Edge Case 2: Dialog is only opened for Staff/Admin — Patient role never
 *              triggers this component (enforced at the call site).
 *
 * UXR-111: Destructive override requires explicit confirmation.
 * UXR-201: WCAG AA contrast on all text/background pairs.
 * UXR-202: Full keyboard navigation; visible focus indicators.
 * UXR-205: aria-describedby links error message to textarea.
 * UXR-501: Confirm button shows spinner and disables during API call.
 */
@Component({
  selector: 'app-override-reason-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './override-reason-dialog.component.html',
  styleUrl: './override-reason-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverrideReasonDialogComponent implements OnDestroy {
  // ── Public constants exposed to template ──────────────────────────────
  readonly maxReasonLength = MAX_REASON_LENGTH;
  readonly charWarningThreshold = CHAR_WARNING_THRESHOLD;

  // ── Injected deps ──────────────────────────────────────────────────────
  private readonly overrideService = inject(OverrideService);
  private readonly snackBar        = inject(MatSnackBar);
  private readonly destroy$        = new Subject<void>();

  // ── Form ───────────────────────────────────────────────────────────────
  readonly form = inject(FormBuilder).nonNullable.group({
    reason: [
      '',
      [
        Validators.required,
        Validators.maxLength(MAX_REASON_LENGTH),
        // AC-3: whitespace-only is invalid — trim-check custom validator
        (ctrl: { value: string }) =>
          ctrl.value?.trim().length === 0 && ctrl.value.length > 0
            ? { whitespaceOnly: true }
            : null,
      ],
    ],
  });

  // ── Signals ────────────────────────────────────────────────────────────
  readonly isSubmitting = signal(false);

  readonly charCount = computed(
    () => this.form.controls.reason.value?.length ?? 0,
  );

  /** True when charCount is in the amber warning zone (480–499). */
  readonly isWarningZone = computed(
    () => this.charCount() >= CHAR_WARNING_THRESHOLD && this.charCount() < MAX_REASON_LENGTH,
  );

  /** True when charCount equals the maximum (500 chars). */
  readonly isAtMax = computed(() => this.charCount() >= MAX_REASON_LENGTH);

  // ── Helpers for template error display ────────────────────────────────
  get reasonCtrl() { return this.form.controls.reason; }

  hasError(errorKey: string): boolean {
    return (
      this.reasonCtrl.touched &&
      (this.reasonCtrl.hasError(errorKey) ||
        // whitespace-only check stored differently
        (errorKey === 'required' && this.reasonCtrl.hasError('whitespaceOnly')))
    );
  }

  constructor(
    readonly dialogRef: MatDialogRef<OverrideReasonDialogComponent, OverrideReasonDialogResult>,
    @Inject(MAT_DIALOG_DATA) readonly data: OverrideReasonDialogData,
  ) {}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSubmit(): void {
    // Mark all controls touched to trigger validation display (AC-3).
    this.form.markAllAsTouched();

    const reason = this.reasonCtrl.value?.trim();
    if (this.form.invalid || !reason) {
      return;
    }

    const payload: OverrideRequest = {
      appointmentId:  this.data.appointmentId,
      constraintType: this.data.constraintType,
      reason,
      action:         this.data.action,
    };

    // UXR-501: disable UI during in-flight request.
    this.isSubmitting.set(true);
    this.form.disable();

    this.overrideService
      .submitOverride(payload)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: OverrideResponse) => {
          this.isSubmitting.set(false);
          this.snackBar.open('Override applied.', 'Dismiss', { duration: 4000 });
          this.dialogRef.close({
            confirmed:     true,
            reason,
            overrideId:    response.overrideId,
            auditRecordId: response.auditRecordId,
          });
        },
        error: (err: { error?: { message?: string } }) => {
          // API failure: keep dialog open for retry; surface the error.
          this.isSubmitting.set(false);
          this.form.enable();
          const msg = err?.error?.message ?? 'Override request failed. Please try again.';
          this.snackBar.open(msg, 'Dismiss', { duration: 6000 });
        },
      });
  }
}
