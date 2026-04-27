import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subject, debounceTime, map, switchMap, takeUntil } from 'rxjs';
import { AiAssistPanelComponent } from '../../components/ai-assist-panel/ai-assist-panel.component';
import { IntakeAssistResponse } from '../../models/intake.model';
import { IntakeApiService } from '../../services/intake.service';
import { BookingApiService } from '../../services/booking-api.service';

type FormState = 'loading' | 'empty' | 'default' | 'error' | 'submitting';

/**
 * Patient Intake Form page — implements SCR-005.
 *
 * States: loading (skeleton), empty (no draft), default (in-progress),
 * error (load failure / retry), submitting (spinner + locked footer).
 *
 * AC-1: AI-assist toggle → free-text panel → API call → field pre-population with badge.
 * AC-2: blur-triggered autosave with 500ms debounce, "Saved" indicator in footer.
 * AC-3: draft restore from GET /draft?slotId on page entry.
 * AC-4: reactive-form validation, submit with loading spinner, double-submit prevention.
 *
 * UXR-104: AI-assist toggle in header.
 * UXR-201/202: WCAG AA contrast, full keyboard navigation.
 * UXR-301: responsive at 375/768/1440px.
 * UXR-405: AI-badge on AI-populated fields.
 * UXR-501: submit button locked during submission.
 * UXR-601: inline errors with icon on touched + invalid.
 */
@Component({
  selector: 'app-intake-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSnackBarModule,
    AiAssistPanelComponent,
  ],
  templateUrl: './intake-form.component.html',
  styleUrl: './intake-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IntakeFormComponent implements OnInit, OnDestroy {
  @ViewChild(AiAssistPanelComponent) aiPanel?: AiAssistPanelComponent;

  private readonly destroy$ = new Subject<void>();
  private readonly autosave$ = new Subject<void>();

  // ── State signals ─────────────────────────────────────────────────────────
  formState = signal<FormState>('loading');
  aiMode = signal(false);
  aiPopulatedFields = signal<string[]>([]);
  autosaveStatus = signal<{ saved: boolean; timestamp?: Date }>({ saved: false });
  draftId = signal<string | null>(null);
  isSubmitting = signal(false);

  slotId: string | null = null;

  readonly severityOptions = ['Mild', 'Moderate', 'Severe'] as const;

  // ── Form ──────────────────────────────────────────────────────────────────
  readonly intakeForm = new FormGroup({
    personalInfo: new FormGroup({
      firstName: new FormControl('', [Validators.required]),
      lastName:  new FormControl('', [Validators.required]),
      dateOfBirth: new FormControl('', [Validators.required]),
      phone: new FormControl('', [Validators.required]),
      email: new FormControl('', [Validators.email]),
    }),
    reasonForVisit: new FormGroup({
      chiefComplaint:    new FormControl('', [Validators.required]),
      symptomDescription: new FormControl(''),
      severity:    new FormControl(''),
      onsetDuration: new FormControl(''),
      bodyArea:    new FormControl(''),
    }),
    medicalHistory: new FormGroup({
      conditions:  new FormControl(''),
      medications: new FormControl(''),
      allergies:   new FormControl(''),
      surgeries:   new FormControl(''),
    }),
    insurance: new FormGroup({
      provider:    new FormControl(''),
      memberId:    new FormControl(''),
      groupNumber: new FormControl(''),
    }),
  });

  // Derived progress for the section-completion progress bar.
  readonly sectionProgress = computed(() => {
    const f = this.intakeForm;
    const completed = [
      f.get('personalInfo')?.valid,
      f.get('reasonForVisit')?.valid,
      f.get('medicalHistory')?.valid,
      f.get('insurance')?.valid,
    ].filter(Boolean).length;
    const total = 4;
    return { completed, total, percentage: (completed / total) * 100 };
  });

  constructor(
    private readonly intakeApi: IntakeApiService,
    private readonly bookingApi: BookingApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.slotId = this.route.snapshot.queryParamMap.get('slotId');

    this.loadDraft(); // AC-3

    // AC-2: debounced autosave triggered from blur events
    this.autosave$
      .pipe(debounceTime(500), takeUntil(this.destroy$))
      .subscribe(() => this.saveDraft());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Draft load / save ─────────────────────────────────────────────────────

  private loadDraft(): void {
    this.formState.set('loading');

    this.intakeApi.getDraft(this.slotId ?? undefined).subscribe({
      next: (draft) => {
        if (draft?.formData) {
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          this.intakeForm.patchValue(draft.formData as any);
          this.draftId.set(draft.id);
          this.aiPopulatedFields.set(draft.aiPopulatedFields ?? []);
          this.formState.set('default');
        } else {
          this.formState.set('empty');
        }
      },
      error: (err) => {
        // 204 is surfaced as an error by HttpClient when the body is empty.
        this.formState.set(err?.status === 204 ? 'empty' : 'error');
      },
    });
  }

  /** Triggered on every field blur event (AC-2). */
  onFieldBlur(): void {
    this.autosave$.next();
  }

  private saveDraft(): void {
    this.intakeApi
      .saveDraft({
        slotId: this.slotId ?? undefined,
        formData: this.intakeForm.getRawValue(),
        aiPopulatedFields: this.aiPopulatedFields(),
      })
      .subscribe({
        next: (result) => {
          this.draftId.set(result.draftId);
          this.autosaveStatus.set({ saved: true, timestamp: new Date(result.savedAt) });
          if (this.formState() === 'empty') this.formState.set('default');
        },
        error: () => {
          this.snackBar.open(
            'Autosave failed. Your changes may not be saved.',
            'Dismiss',
            { duration: 5000, panelClass: 'error-snackbar' },
          );
        },
      });
  }

  // ── AI assist ─────────────────────────────────────────────────────────────

  onAiToggle(enabled: boolean): void {
    this.aiMode.set(enabled);
  }

  /** Called when the AI panel emits the free-text string (AC-1). */
  onAiSubmit(freeText: string): void {
    this.aiPanel?.setProcessing(true);

    this.intakeApi.aiAssist({ freeTextDescription: freeText }).subscribe({
      next: (response: IntakeAssistResponse) => {
        this.aiPanel?.setProcessing(false);

        if (response.aiAssisted && response.suggestions) {
          const s = response.suggestions;
          this.intakeForm.patchValue({
            reasonForVisit: {
              chiefComplaint:     s.reasonForVisit ?? '',
              symptomDescription: s.symptomDescription ?? '',
              severity:           s.severity ?? '',
              onsetDuration:      s.onsetDuration ?? '',
              bodyArea:           s.bodyArea ?? '',
            },
            medicalHistory: {
              conditions:  s.relevantMedicalHistory?.join(', ') ?? '',
              medications: s.currentMedications?.join(', ') ?? '',
              allergies:   s.allergies?.join(', ') ?? '',
            },
          });

          this.aiPopulatedFields.set(response.aiPopulatedFields);
          this.autosave$.next(); // persist AI-populated data

          this.snackBar.open(
            'AI suggestions applied. Review and edit as needed.',
            'OK',
            { duration: 4000 },
          );
        } else {
          // Fallback: revert to manual mode (edge case / AIR-005)
          this.aiMode.set(false);
          this.snackBar.open(
            response.fallbackReason ?? 'AI assist unavailable, please fill in manually.',
            'OK',
            { duration: 5000, panelClass: 'warning-snackbar' },
          );
        }
      },
      error: () => {
        this.aiPanel?.setProcessing(false);
        this.aiMode.set(false);
        this.snackBar.open(
          'AI assist unavailable, please fill in manually.',
          'OK',
          { duration: 5000, panelClass: 'warning-snackbar' },
        );
      },
    });
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  onSubmit(): void {
    this.intakeForm.markAllAsTouched();

    if (this.intakeForm.invalid || this.isSubmitting()) return;

    const draftId = this.draftId();
    if (!draftId) {
      this.snackBar.open('Please wait — form is still saving. Try again in a moment.', 'Dismiss', {
        duration: 5000, panelClass: 'error-snackbar',
      });
      return;
    }

    if (!this.slotId) {
      this.snackBar.open('No appointment slot selected. Please go back and choose a slot.', 'Dismiss', {
        duration: 5000, panelClass: 'error-snackbar',
      });
      return;
    }

    this.isSubmitting.set(true);
    this.formState.set('submitting');

    // Step 1: Create the booking (reserves the slot atomically).
    this.bookingApi.createBooking({ slotId: this.slotId }).pipe(
      switchMap((booking) =>
        // Step 2: Submit the intake form attached to the new appointment.
        this.intakeApi.submitIntake({ draftId, appointmentId: booking.appointmentId }).pipe(
          map(() => booking.appointmentId),
        )
      ),
    ).subscribe({
      next: (appointmentId) => {
        this.isSubmitting.set(false);
        this.router.navigate(['/scheduling/booking/confirmation', appointmentId]);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.formState.set('default');
        if (err?.status === 409) {
          this.snackBar.open(
            'This slot was just taken. Please go back and select another.',
            'Dismiss',
            { duration: 6000, panelClass: 'error-snackbar' },
          );
        } else {
          const message = err?.error?.title ?? 'Submission failed. Please try again.';
          this.snackBar.open(message, 'Dismiss', {
            duration: 5000, panelClass: 'error-snackbar',
          });
        }
      },
    });
  }

  onBack(): void {
    this.saveDraft();
    this.router.navigate(['/scheduling/search']);
  }

  onRetry(): void {
    this.loadDraft();
  }

  /** True when the given field name was populated by AI (UXR-405). */
  isAiPopulated(fieldName: string): boolean {
    return this.aiPopulatedFields().includes(fieldName);
  }
}
