import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { Subject, Subscription } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { WalkinService } from './walkin.service';
import { PatientSearchService } from './patient-search.service';
import { PatientSearchResult } from '../../shared/models/patient-search-result.model';

/** Phone pattern — optional international prefix, digits, spaces, dashes, parens. */
const PHONE_PATTERN = /^\+?[\d\s\-()+]{7,20}$/;


/**
 * Walk-In Registration form (EP-004 US_033 SCR-029).
 *
 * AC-1: Staff creates a walk-in entry with name, phone, and visit reason.
 * AC-2: "Convert to Patient" toggle enables inline registration fields.
 * AC-3: Walk-in entries displayed with "Walk-In" badge on the queue dashboard.
 * AC-4: Debounced patient search finds existing accounts; selecting one links
 *        the walk-in to that account without duplicating the patient record.
 *
 * Edge Case 1: Disambiguation list shown when multiple patients match.
 * Edge Case 2: Capacity warning banner shown when queue ≥ CAPACITY_THRESHOLD.
 *
 * UXR-201: WCAG AA contrast on all text/background pairs.
 * UXR-202: Full keyboard navigation; visible focus indicators.
 * UXR-205: aria-describedby on all validated inputs.
 * UXR-301: Responsive — compact single-column (max-width: 480px).
 * UXR-501: Submit button shows spinner and disables during in-flight POST.
 */
@Component({
  selector: 'app-walkin-registration',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './walkin-registration.component.html',
  styleUrl: './walkin-registration.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WalkinRegistrationComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly walkinService = inject(WalkinService);
  private readonly patientSearchService = inject(PatientSearchService);
  private readonly snackBar = inject(MatSnackBar);
  readonly router = inject(Router);

  // ── State signals ──────────────────────────────────────────────────────────
  readonly isSubmitting = signal(false);
  readonly searchResults = signal<PatientSearchResult[]>([]);
  readonly isSearching = signal(false);
  readonly selectedPatient = signal<PatientSearchResult | null>(null);
  readonly atCapacity = signal(false);
  readonly showDisambiguation = signal(false);

  // ── Patient search subject (debounced via PatientSearchService) ────────────
  readonly searchQuery$ = new Subject<string>();

  private readonly subscriptions = new Subscription();

  // ── Reactive form ──────────────────────────────────────────────────────────
  readonly form = this.fb.nonNullable.group({
    patientName: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['', [Validators.pattern(PHONE_PATTERN)]],
    visitReason: ['', [Validators.required, Validators.maxLength(500)]],
    convertToPatient: [false],
    dateOfBirth: [''],
    email: [''],
  });

  ngOnInit(): void {
    // ── Debounced patient search ───────────────────────────────────────────
    this.subscriptions.add(
      this.patientSearchService.results$(this.searchQuery$).subscribe({
        next: (results) => {
          this.searchResults.set(results);
          this.showDisambiguation.set(results.length > 0);
          this.isSearching.set(false);
        },
        error: () => {
          this.isSearching.set(false);
        },
      }),
    );

    // When "Convert to Patient" is toggled, add/remove validators dynamically.
    this.subscriptions.add(
      this.form.controls.convertToPatient.valueChanges.subscribe((enabled) => {
        const dobCtrl = this.form.controls.dateOfBirth;
        const emailCtrl = this.form.controls.email;
        if (enabled) {
          dobCtrl.addValidators([Validators.required]);
          emailCtrl.addValidators([Validators.required, Validators.email]);
        } else {
          dobCtrl.clearValidators();
          emailCtrl.clearValidators();
        }
        dobCtrl.updateValueAndValidity();
        emailCtrl.updateValueAndValidity();
      }),
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.searchQuery$.complete();
  }

  // ── Template helpers ───────────────────────────────────────────────────────

  /** Invoked on keyup in the patient name field to trigger debounced search. */
  onNameInput(value: string): void {
    if (!this.selectedPatient()) {
      this.isSearching.set(value.length >= 2);
      this.searchQuery$.next(value);
    }
  }

  /** AC-4: User selects a patient from the disambiguation list. */
  selectPatient(patient: PatientSearchResult): void {
    this.selectedPatient.set(patient);
    this.form.controls.patientName.setValue(patient.name);
    this.form.controls.phone.setValue(patient.phone ?? '');
    this.showDisambiguation.set(false);
    this.searchResults.set([]);
  }

  /** Clear selected patient and allow re-search. */
  clearSelectedPatient(): void {
    this.selectedPatient.set(null);
    this.form.controls.patientName.setValue('');
    this.form.controls.phone.setValue('');
  }

  /** Returns true when convertToPatient is toggled on. */
  get convertEnabled(): boolean {
    return this.form.controls.convertToPatient.value;
  }

  /** Returns form control for use in template error binding. */
  ctrl(name: keyof typeof this.form.controls): AbstractControl {
    return this.form.controls[name];
  }

  /** Returns true when a control is invalid and dirty/touched. */
  hasError(name: keyof typeof this.form.controls, error: string): boolean {
    const c = this.form.controls[name];
    return c.hasError(error) && (c.dirty || c.touched);
  }

  // ── Submit ─────────────────────────────────────────────────────────────────
  onSubmit(): void {
    if (this.form.invalid || this.isSubmitting()) return;

    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.isSubmitting.set(true);

    const raw = this.form.getRawValue();
    this.subscriptions.add(
      this.walkinService
        .createWalkin({
          patientName: raw.patientName,
          phone: raw.phone || undefined,
          visitReason: raw.visitReason,
          existingPatientId: this.selectedPatient()?.id,
          convertToPatient: raw.convertToPatient,
          dateOfBirth: raw.convertToPatient ? raw.dateOfBirth || undefined : undefined,
          email: raw.convertToPatient ? raw.email || undefined : undefined,
        })
        .subscribe({
          next: (response) => {
            this.isSubmitting.set(false);
            this.atCapacity.set(response.atCapacity);
            this.snackBar.open(
              `Walk-in added — Queue position #${response.queuePosition}`,
              'Close',
              { duration: 5000 },
            );
            void this.router.navigate(['/staff/queue']);
          },
          error: (err) => {
            this.isSubmitting.set(false);
            const message: string =
              err?.error?.message ?? 'Failed to add walk-in. Please try again.';
            this.snackBar.open(message, 'Dismiss', { duration: 0 });
          },
        }),
    );
  }
}
