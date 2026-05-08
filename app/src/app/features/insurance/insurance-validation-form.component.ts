import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, finalize } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';

import { InsuranceService } from './insurance.service';
import { InsuranceFormData, InsuranceTier } from '../../shared/models/insurance-form-data.model';
import {
  InsuranceValidationResult,
  InsuranceValidationStatus,
} from '../../shared/models/insurance-validation-result.model';

/** Policy number: alphanumeric + hyphens, 5–30 chars (AC-1). */
const POLICY_PATTERN = /^[A-Za-z0-9\-]{5,30}$/;

/** Accepted MIME types for card image uploads (UXR-505). */
const ACCEPTED_MIME = ['image/jpeg', 'image/png'];

/** Maximum card image size in bytes (5 MB, UXR-505). */
const MAX_CARD_FILE_SIZE = 5 * 1024 * 1024;

/** Validator: policy number must match the expected alphanumeric format. */
function policyFormatValidator(
  ctrl: AbstractControl,
): ValidationErrors | null {
  const v = ctrl.value as string | null;
  if (!v) return null;
  return POLICY_PATTERN.test(v) ? null : { policyFormat: true };
}

/**
 * Insurance Soft Validation Form (EP-005 US_037 SCR-028).
 *
 * AC-1: Policy number format and provider code are validated within 500 ms.
 * AC-2: Format-mismatch warnings are non-blocking — booking always continues.
 * AC-3: SoftValidated result → green "Verified" badge.
 * AC-4: ValidationFailed → red badge + flagged for staff review.
 * Edge Case 1: Reference DB unavailable → ValidationPending info banner.
 * Edge Case 2: Secondary policy matches primary → non-blocking warning banner.
 *
 * UXR-201: WCAG AA contrast on all text/background pairs.
 * UXR-202: Full keyboard navigation; visible focus indicators.
 * UXR-205: aria-describedby links error messages to form fields.
 * UXR-301: Responsive at 375 / 768 / 1440 px.
 * UXR-404: green=success, amber=warning, red=error, blue=info.
 * UXR-501: Submit button shows spinner and disables during network request.
 * UXR-505: Card image upload with drag-and-drop and progress indication.
 */
@Component({
  selector: 'app-insurance-validation-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatSnackBarModule,
  ],
  templateUrl: './insurance-validation-form.component.html',
  styleUrl: './insurance-validation-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InsuranceValidationFormComponent implements OnInit {
  private readonly insuranceService = inject(InsuranceService);
  private readonly snackBar         = inject(MatSnackBar);
  private readonly destroyRef       = inject(DestroyRef);
  private readonly fb               = inject(FormBuilder);

  // ── Visibility ────────────────────────────────────────────────────────
  /** True when at least one insurance was saved — hides the empty-state CTA. */
  protected readonly hasInsurance = signal(false);
  /** Tracks whether the user has acknowledged the empty state and pressed Add. */
  protected readonly showForm = signal(true);
  /** True while the validate + save network call is in flight (UXR-501). */
  protected readonly isSubmitting = signal(false);
  /** True when the secondary insurance toggle is on. */
  protected readonly showSecondary = signal(false);

  // ── Validation results ────────────────────────────────────────────────
  protected readonly primaryResult   = signal<InsuranceValidationResult | null>(null);
  protected readonly secondaryResult = signal<InsuranceValidationResult | null>(null);

  /** True when both primary insurance numbers are identical (Edge Case 2). */
  protected readonly duplicatePolicyWarning = computed(() => {
    if (!this.showSecondary()) return false;
    const pPrimary   = this.primaryForm.value.policyNumber?.trim();
    const pSecondary = this.secondaryForm.value.policyNumber?.trim();
    return (
      !!pPrimary && !!pSecondary && pPrimary === pSecondary
    );
  });

  // ── Card image state ───────────────────────────────────────────────────
  protected readonly primaryFrontFile  = signal<File | null>(null);
  protected readonly primaryBackFile   = signal<File | null>(null);
  protected readonly secondaryFrontFile = signal<File | null>(null);
  protected readonly secondaryBackFile  = signal<File | null>(null);
  protected readonly uploadError        = signal<string | null>(null);

  // ── Forms ─────────────────────────────────────────────────────────────
  protected readonly primaryForm = this.fb.nonNullable.group({
    policyNumber: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(30), policyFormatValidator]],
    providerCode: ['', [Validators.required, Validators.maxLength(50)]],
    providerName: ['', [Validators.required, Validators.maxLength(100)]],
    groupNumber:  ['', [Validators.maxLength(50)]],
  });

  protected readonly secondaryForm = this.fb.nonNullable.group({
    policyNumber: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(30), policyFormatValidator]],
    providerCode: ['', [Validators.required, Validators.maxLength(50)]],
    providerName: ['', [Validators.required, Validators.maxLength(100)]],
    groupNumber:  ['', [Validators.maxLength(50)]],
  });

  ngOnInit(): void {
    // Trigger duplicate-policy recompute on either form change.
    this.primaryForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => { /* computed() auto-updates */ });

    this.secondaryForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => { /* computed() auto-updates */ });
  }

  // ── Template helpers ──────────────────────────────────────────────────

  protected toggleSecondary(checked: boolean): void {
    this.showSecondary.set(checked);
    if (!checked) {
      this.secondaryForm.reset();
      this.secondaryResult.set(null);
      this.secondaryFrontFile.set(null);
      this.secondaryBackFile.set(null);
    }
  }

  protected startFresh(): void {
    this.showForm.set(true);
  }

  /** Returns the badge/banner variant label for the result (UXR-404). */
  protected statusLabel(result: InsuranceValidationResult | null): string {
    if (!result) return '';
    const map: Record<InsuranceValidationStatus, string> = {
      SoftValidated:     'Verified',
      Warning:           'Insurance details may be incomplete',
      ValidationFailed:  'Validation failed — flagged for staff review',
      ValidationPending: 'Validation pending — booking will continue',
    };
    return map[result.status];
  }

  // ── File upload (UXR-505) ─────────────────────────────────────────────

  protected onFrontFileChange(event: Event, tier: InsuranceTier): void {
    const file = this.extractFile(event);
    if (!file) return;
    tier === 'Primary'
      ? this.primaryFrontFile.set(file)
      : this.secondaryFrontFile.set(file);
  }

  protected onBackFileChange(event: Event, tier: InsuranceTier): void {
    const file = this.extractFile(event);
    if (!file) return;
    tier === 'Primary'
      ? this.primaryBackFile.set(file)
      : this.secondaryBackFile.set(file);
  }

  protected onFileDrop(event: DragEvent, slot: 'primaryFront' | 'primaryBack' | 'secondaryFront' | 'secondaryBack'): void {
    event.preventDefault();
    const file = event.dataTransfer?.files?.[0] ?? null;
    if (!file || !this.validateFile(file)) return;
    if (slot === 'primaryFront')    this.primaryFrontFile.set(file);
    if (slot === 'primaryBack')     this.primaryBackFile.set(file);
    if (slot === 'secondaryFront')  this.secondaryFrontFile.set(file);
    if (slot === 'secondaryBack')   this.secondaryBackFile.set(file);
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  // ── Submit ─────────────────────────────────────────────────────────────

  protected onSubmit(): void {
    this.primaryForm.markAllAsTouched();
    if (this.primaryForm.invalid) return;
    if (this.showSecondary()) this.secondaryForm.markAllAsTouched();

    this.isSubmitting.set(true);
    this.uploadError.set(null);

    const primaryPayload: InsuranceFormData = {
      tier:           'Primary',
      policyNumber:   this.primaryForm.value.policyNumber!,
      providerCode:   this.primaryForm.value.providerCode!,
      providerName:   this.primaryForm.value.providerName!,
      groupNumber:    this.primaryForm.value.groupNumber || undefined,
      cardImageFront: this.primaryFrontFile() ?? undefined,
      cardImageBack:  this.primaryBackFile()  ?? undefined,
    };

    // Step 1: Validate primary (AC-1).
    this.insuranceService
      .validate(primaryPayload)
      .pipe(
        catchError(() => {
          // Edge Case 1: treat any network error as ValidationPending.
          this.primaryResult.set({
            status:           'ValidationPending',
            warnings:         [],
            providerMatch:    false,
            policyFormatValid: false,
            message:          'Reference database unavailable. Booking continues.',
          });
          return EMPTY;
        }),
        finalize(() => {
          if (!this.showSecondary()) this.isSubmitting.set(false);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.primaryResult.set(result);

        // Step 2: Save primary record regardless of validation outcome (AC-2).
        this.persistInsurance(primaryPayload, 'primary');
      });

    // Secondary (if toggled on and valid).
    if (this.showSecondary() && this.secondaryForm.valid) {
      const secondaryPayload: InsuranceFormData = {
        tier:           'Secondary',
        policyNumber:   this.secondaryForm.value.policyNumber!,
        providerCode:   this.secondaryForm.value.providerCode!,
        providerName:   this.secondaryForm.value.providerName!,
        groupNumber:    this.secondaryForm.value.groupNumber || undefined,
        cardImageFront: this.secondaryFrontFile() ?? undefined,
        cardImageBack:  this.secondaryBackFile()  ?? undefined,
      };

      this.insuranceService
        .validate(secondaryPayload)
        .pipe(
          catchError(() => {
            this.secondaryResult.set({
              status:           'ValidationPending',
              warnings:         [],
              providerMatch:    false,
              policyFormatValid: false,
            });
            return EMPTY;
          }),
          finalize(() => this.isSubmitting.set(false)),
          takeUntilDestroyed(this.destroyRef),
        )
        .subscribe((result) => {
          this.secondaryResult.set(result);
          this.persistInsurance(secondaryPayload, 'secondary');
        });
    }
  }

  // ── Private ───────────────────────────────────────────────────────────

  private persistInsurance(
    payload: InsuranceFormData,
    tier: 'primary' | 'secondary',
  ): void {
    this.insuranceService
      .save(payload)
      .pipe(
        catchError(() => {
          this.snackBar.open(
            `Failed to save ${tier} insurance. Please try again.`,
            'Dismiss',
            { duration: 5000, panelClass: 'snack-error' },
          );
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.hasInsurance.set(true);
        this.snackBar.open(
          `${tier === 'primary' ? 'Primary' : 'Secondary'} insurance saved.`,
          undefined,
          { duration: 3000, panelClass: 'snack-success' },
        );
      });
  }

  private extractFile(event: Event): File | null {
    const input = event.target as HTMLInputElement;
    const file  = input.files?.[0] ?? null;
    if (file && !this.validateFile(file)) return null;
    return file;
  }

  private validateFile(file: File): boolean {
    if (!ACCEPTED_MIME.includes(file.type)) {
      this.uploadError.set('Only JPEG and PNG images are accepted.');
      return false;
    }
    if (file.size > MAX_CARD_FILE_SIZE) {
      this.uploadError.set('Card image must be 5 MB or smaller.');
      return false;
    }
    this.uploadError.set(null);
    return true;
  }
}
