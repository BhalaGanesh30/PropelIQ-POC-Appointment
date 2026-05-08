import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
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
import { Router } from '@angular/router';
import { Subject, Subscription } from 'rxjs';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatStepperModule } from '@angular/material/stepper';

import { PatientSearchService } from '../walkin/patient-search.service';
import { SlotSearchService } from './slot-search.service';
import { StaffBookingService } from './staff-booking.service';
import { TokenStorageService } from '../../core/services/token-storage.service';
import { PatientSearchResult } from '../../shared/models/patient-search-result.model';
import { SlotResult, ConflictCheck } from '../../shared/models/slot-result.model';
import { StaffBookingRequest, InlinePatientForm } from '../../shared/models/staff-booking-request.model';

/** Validator: rejects whitespace-only strings. */
function noWhitespaceOnly(control: AbstractControl): ValidationErrors | null {
  const value: unknown = control.value;
  if (typeof value === 'string' && value.trim().length === 0 && value.length > 0) {
    return { whitespaceOnly: true };
  }
  return null;
}

const PHONE_PATTERN = /^\+?[\d\s\-()+]{7,20}$/;

/**
 * Staff-Assisted Booking Wizard (EP-004 US_035 SCR-027).
 *
 * AC-1: Staff searches for an existing patient (debounced, ≥2 chars).
 *        Own profile (self-booking) is excluded from results (Edge Case 2).
 * AC-2: Booking is attributed to the acting staff user ID (JWT) in the audit log.
 * AC-3: Staff may create a new patient profile inline when not found in search.
 * AC-4: Override reason is mandatory (max 300 chars) when a slot conflict was
 *        acknowledged by staff (Edge Case 1).
 *
 * Edge Case 1: Conflict detected — staff must acknowledge and provide a reason.
 * Edge Case 2: Staff cannot book for themselves; own ID excluded from search results.
 *
 * UXR-201: WCAG AA contrast on all text/background pairs.
 * UXR-202: Full keyboard navigation; visible focus indicators.
 * UXR-205: aria-describedby on all validated inputs.
 * UXR-501: Submit button shows spinner and disables during in-flight POST.
 */
@Component({
  selector: 'app-staff-booking-wizard',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    MatStepperModule,
  ],
  templateUrl: './staff-booking-wizard.component.html',
  styleUrl: './staff-booking-wizard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffBookingWizardComponent implements OnInit, OnDestroy {
  // ── Dependencies ────────────────────────────────────────────────────────────
  readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly patientSearch = inject(PatientSearchService);
  private readonly slotSearch = inject(SlotSearchService);
  private readonly staffBookingService = inject(StaffBookingService);
  private readonly tokenStorage = inject(TokenStorageService);

  // ── State ────────────────────────────────────────────────────────────────────
  readonly currentStep = signal<1 | 2 | 3 | 4>(1);
  readonly selectedPatient = signal<PatientSearchResult | null>(null);
  readonly selectedSlot = signal<SlotResult | null>(null);
  readonly conflict = signal<ConflictCheck | null>(null);
  readonly conflictAcknowledged = signal(false);
  readonly showNewPatientForm = signal(false);
  readonly isSubmitting = signal(false);
  readonly isLoadingSlots = signal(false);
  readonly isCheckingConflict = signal(false);

  readonly searchResults = signal<PatientSearchResult[]>([]);
  readonly slots = signal<SlotResult[]>([]);
  readonly searchError = signal<string | null>(null);
  readonly slotsError = signal<string | null>(null);

  /** True when a conflict was detected and staff has not yet acknowledged it. */
  readonly pendingConflictWarning = computed(
    () => this.conflict()?.hasConflict === true && !this.conflictAcknowledged(),
  );

  /** Override reason is required only when conflict was acknowledged. */
  readonly overrideRequired = computed(() => this.conflictAcknowledged());

  // ── Forms ────────────────────────────────────────────────────────────────────
  readonly searchForm = this.fb.group({
    query: ['', [Validators.minLength(2)]],
  });

  readonly newPatientForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100), noWhitespaceOnly]],
    lastName: ['', [Validators.required, Validators.maxLength(100), noWhitespaceOnly]],
    phone: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
    dateOfBirth: ['', [Validators.required]],
    email: ['', [Validators.email, Validators.maxLength(200)]],
  });

  readonly slotFilterForm = this.fb.group({
    date: [new Date().toISOString().slice(0, 10), [Validators.required]],
    duration: [30, [Validators.required]],
    providerName: [''],
  });

  readonly intakeForm = this.fb.group({
    visitReason: ['', [Validators.required, Validators.maxLength(500), noWhitespaceOnly]],
    overrideReason: ['', [Validators.maxLength(300)]],
  });

  // ── Computed char counts ─────────────────────────────────────────────────────
  readonly visitReasonLen = computed(
    () => (this.intakeForm.get('visitReason')?.value ?? '').length,
  );
  readonly overrideReasonLen = computed(
    () => (this.intakeForm.get('overrideReason')?.value ?? '').length,
  );
  readonly visitReasonWarning = computed(() => this.visitReasonLen() >= 480);
  readonly visitReasonAtMax = computed(() => this.visitReasonLen() >= 500);
  readonly overrideReasonWarning = computed(() => this.overrideReasonLen() >= 280);
  readonly overrideReasonAtMax = computed(() => this.overrideReasonLen() >= 300);

  // ── Internal ─────────────────────────────────────────────────────────────────
  private readonly searchQuery$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();
  private searchSub?: Subscription;
  private slotsSub?: Subscription;
  private conflictSub?: Subscription;

  /** UUID of the logged-in staff user — excluded from search results (Edge Case 2). */
  private readonly currentUserId =
    (this.tokenStorage.getDecodedToken?.()?.['sub'] as string | undefined) ?? null;

  // ── Lifecycle ────────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.searchSub = this.patientSearch.results$(this.searchQuery$).subscribe({
      next: (results) => {
        // AC-1 / Edge Case 2: exclude own profile
        const filtered = this.currentUserId
          ? results.filter((p) => p.id !== this.currentUserId)
          : results;
        this.searchResults.set(filtered);
        this.searchError.set(null);
      },
      error: () => this.searchError.set('Search failed. Please try again.'),
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.searchSub?.unsubscribe();
    this.slotsSub?.unsubscribe();
    this.conflictSub?.unsubscribe();
  }

  // ── Step 1: Patient search ────────────────────────────────────────────────────
  onSearchInput(value: string): void {
    this.searchQuery$.next(value);
  }

  selectPatient(patient: PatientSearchResult): void {
    this.selectedPatient.set(patient);
    this.showNewPatientForm.set(false);
  }

  clearPatient(): void {
    this.selectedPatient.set(null);
    this.showNewPatientForm.set(false);
    this.searchForm.reset();
  }

  toggleNewPatientForm(): void {
    this.showNewPatientForm.set(!this.showNewPatientForm());
    this.selectedPatient.set(null);
    if (!this.showNewPatientForm()) {
      this.newPatientForm.reset();
    }
  }

  proceedToSlots(): void {
    const hasExistingPatient = this.selectedPatient() !== null;
    const hasNewPatient = this.showNewPatientForm() && this.newPatientForm.valid;
    if (!hasExistingPatient && !hasNewPatient) return;

    this.currentStep.set(2);
    this.loadSlots();
  }

  // ── Step 2: Slot selection ────────────────────────────────────────────────────
  loadSlots(): void {
    const { date, duration } = this.slotFilterForm.value;
    if (!date || !duration) return;

    this.isLoadingSlots.set(true);
    this.slotsError.set(null);
    this.slotsSub?.unsubscribe();

    this.slotsSub = this.slotSearch.searchSlots(date, duration).subscribe({
      next: (slots) => {
        this.slots.set(slots);
        this.isLoadingSlots.set(false);
      },
      error: () => {
        this.slotsError.set('Failed to load slots. Please try again.');
        this.isLoadingSlots.set(false);
      },
    });
  }

  selectSlot(slot: SlotResult): void {
    if (!slot.available) return;

    this.selectedSlot.set(slot);
    this.conflict.set(null);
    this.conflictAcknowledged.set(false);

    const patientId = this.selectedPatient()?.id;
    if (!patientId) return;

    this.isCheckingConflict.set(true);
    this.conflictSub?.unsubscribe();

    this.conflictSub = this.slotSearch.checkConflict(patientId, slot.slotId).subscribe({
      next: (result) => {
        this.conflict.set(result);
        this.isCheckingConflict.set(false);
      },
      error: () => {
        // Non-fatal: proceed without conflict data rather than blocking the user.
        this.isCheckingConflict.set(false);
      },
    });
  }

  acknowledgeConflict(): void {
    this.conflictAcknowledged.set(true);
    this.intakeForm.get('overrideReason')?.setValidators([
      Validators.required,
      Validators.maxLength(300),
      noWhitespaceOnly,
    ]);
    this.intakeForm.get('overrideReason')?.updateValueAndValidity();
  }

  proceedToIntake(): void {
    if (!this.selectedSlot()) return;
    if (this.pendingConflictWarning()) return;
    this.currentStep.set(3);
  }

  // ── Step 3 / 4 ────────────────────────────────────────────────────────────────
  proceedToConfirm(): void {
    if (this.intakeForm.invalid) {
      this.intakeForm.markAllAsTouched();
      return;
    }
    this.currentStep.set(4);
  }

  goBack(): void {
    const step = this.currentStep();
    if (step > 1) {
      this.currentStep.set((step - 1) as 1 | 2 | 3 | 4);
    }
  }

  // ── Step 4: Submit ────────────────────────────────────────────────────────────
  confirmBooking(): void {
    if (this.isSubmitting()) return;

    const slot = this.selectedSlot();
    if (!slot) return;

    const { visitReason, overrideReason } = this.intakeForm.getRawValue();
    const existingPatient = this.selectedPatient();
    const newPatientRaw = this.showNewPatientForm() ? this.newPatientForm.getRawValue() : null;

    const payload: StaffBookingRequest = {
      slotId: slot.slotId,
      visitReason: visitReason ?? '',
      overrideReason: overrideReason || undefined,
      patientId: existingPatient?.id,
      newPatient: newPatientRaw
        ? ({
            firstName: newPatientRaw.firstName ?? '',
            lastName: newPatientRaw.lastName ?? '',
            phone: newPatientRaw.phone ?? '',
            dateOfBirth: newPatientRaw.dateOfBirth ?? '',
            email: newPatientRaw.email ?? undefined,
          } satisfies InlinePatientForm)
        : undefined,
    };

    this.isSubmitting.set(true);

    this.staffBookingService.createBooking(payload).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open('Booking confirmed successfully.', 'Dismiss', { duration: 5000 });
        this.router.navigate(['/appointments']);
      },
      error: (err: { error?: { message?: string } }) => {
        this.isSubmitting.set(false);
        const message = err?.error?.message ?? 'Booking failed. Please try again.';
        this.snackBar.open(message, 'Dismiss', { duration: 7000 });
      },
    });
  }

  // ── Template helpers ──────────────────────────────────────────────────────────
  /** Step 1 "Next" is enabled when a patient is selected or the new-patient form is valid. */
  readonly canProceedFromStep1 = computed(
    () =>
      this.selectedPatient() !== null ||
      (this.showNewPatientForm() && this.newPatientForm.valid),
  );

  /** Step 2 "Next" is enabled when a slot is selected and no unacknowledged conflict. */
  readonly canProceedFromStep2 = computed(
    () => this.selectedSlot() !== null && !this.pendingConflictWarning() && !this.isCheckingConflict(),
  );
}
