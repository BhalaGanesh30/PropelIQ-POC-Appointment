# Task - TASK_003

## Requirement Reference

- User Story: us_020
- Story Location: .propel/context/tasks/EP-002/us_020/us_020.md
- Acceptance Criteria:
  - AC-1: Given I am on the intake form, When I toggle to AI-assisted mode and provide a free-text description of my symptoms, Then the AI suggests structured intake fields pre-populated from my description within 2.5 seconds.
  - AC-2: Given I am filling in the intake form, When I move focus away from a field (blur event), Then the system autosaves my draft and displays a "Saved" indicator within 1 second.
  - AC-3: Given I navigate away from the intake form without submitting, When I return to the booking flow, Then my saved draft is restored and I can continue from where I left off.
  - AC-4: Given I am in manual mode, When I complete all required fields and submit, Then the intake data is validated and attached to the booking record.
- Edge Cases:
  - What happens if the AI-assist call fails or times out? The form falls back to manual mode with a notification: "AI assist unavailable, please fill in manually."
  - How does the system handle unsaved drafts after session expiry? Draft is associated with the patient account and retained for 7 days post-session, not lost on timeout.

## Design References (Frontend Tasks Only)

> **Note**: The user story references `SCR-007` but the correct Figma screen for the intake form is `SCR-005: Intake Form` per figma_spec.md. SCR-007 is "Appointment History." This task implements SCR-005.

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-005-intake-form.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | figma_spec.md#SCR-005 |
| **UXR Requirements** | UXR-104, UXR-201, UXR-202, UXR-205, UXR-301, UXR-405, UXR-501, UXR-601 |
| **Design Tokens** | designsystem.md — colors, typography, spacing |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | N/A (consumed via API) | N/A |
| Database | N/A | N/A |
| Library | Angular Material | 17.x |
| Library | Angular Reactive Forms | 17.x (bundled) |
| Library | @angular/router | 17.x (bundled) |
| Library | rxjs | 7.x |
| AI/ML | N/A (consumed via API) | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Build the Angular 17 intake form page implementing SCR-005 with four sections (personal info, reason for visit, medical history, insurance reference), an AI-assist toggle in the header, autosave on blur, and draft restoration. The page renders as a single-column form (max-width 720px) with a sticky bottom bar containing Back and Submit buttons and an autosave status indicator (SCR-005 layout). The AI-assist toggle (UXR-104) switches between manual entry and AI-assisted mode: in AI mode, a free-text textarea appears where the patient describes symptoms; on submit the AI-assist API is called and returned suggestions pre-populate form fields with a visual badge distinguishing AI-generated values (UXR-405). Each field emits a blur-triggered autosave via `PUT /api/v1/intake/draft`, debounced at 500ms, with a "Saved" indicator showing timestamp (AC-2). On page load, `GET /api/v1/intake/draft?slotId={id}` restores any existing draft (AC-3). On final submit, all required fields are validated client-side with inline errors (UXR-205, UXR-601) before calling `POST /api/v1/intake/submit` (AC-4) with loading spinner and double-submit prevention (UXR-501). When AI-assist fails, the toggle automatically reverts to manual mode with a snackbar notification (edge case). The form supports full keyboard navigation (UXR-202), WCAG AA contrast (UXR-201), and responsive layout across 375px/768px/1440px breakpoints (UXR-301). All five SCR-005 states are implemented: Default, Loading, Empty, Error, and Validation.

## Dependent Tasks

- US_020 task_001 (requires backend intake draft API: PUT /draft, GET /draft, POST /submit)
- US_020 task_002 (requires AI-assist endpoint: POST /ai-assist)
- US_019 task_002 (provides slot selection context — slotId passed via route query param)

## Impacted Components

- New: `client/src/app/features/scheduling/pages/intake-form/intake-form.component.ts` (main intake page with form, AI toggle, autosave, states)
- New: `client/src/app/features/scheduling/pages/intake-form/intake-form.component.html` (template with multi-section form, AI toggle header, sticky footer)
- New: `client/src/app/features/scheduling/pages/intake-form/intake-form.component.scss` (single-column layout, AI badge styles, autosave indicator, responsive)
- New: `client/src/app/features/scheduling/components/ai-assist-panel/ai-assist-panel.component.ts` (AI-mode free-text input and suggestion trigger)
- New: `client/src/app/features/scheduling/components/ai-assist-panel/ai-assist-panel.component.html` (AI panel template)
- New: `client/src/app/features/scheduling/components/ai-assist-panel/ai-assist-panel.component.scss` (AI panel styles)
- New: `client/src/app/features/scheduling/services/intake.service.ts` (API client for draft save, retrieve, submit, AI-assist)
- New: `client/src/app/features/scheduling/models/intake.model.ts` (TypeScript interfaces for intake DTOs)
- Modify: `client/src/app/features/scheduling/scheduling-routing.module.ts` (add intake form route)

## Implementation Plan

1. **Create TypeScript models** for intake DTOs:

```typescript
// client/src/app/features/scheduling/models/intake.model.ts
export interface SaveDraftRequest {
  slotId?: string;
  formData: Record<string, unknown>;
  aiPopulatedFields?: string[];
}

export interface SaveDraftResponse {
  draftId: string;
  savedAt: string;
}

export interface IntakeDraftResponse {
  id: string;
  slotId?: string;
  formData: Record<string, unknown>;
  aiPopulatedFields: string[];
  status: string;
  updatedAt: string;
}

export interface SubmitIntakeRequest {
  draftId: string;
  appointmentId: string;
}

export interface SubmitIntakeResponse {
  intakeRecordId: string;
  submittedAt: string;
}

export interface IntakeAssistRequest {
  freeTextDescription: string;
  language?: string;
}

export interface IntakeAssistResponse {
  aiAssisted: boolean;
  fallbackReason?: string;
  suggestions: IntakeFieldSuggestions;
  aiPopulatedFields: string[];
  confidence: number;
}

export interface IntakeFieldSuggestions {
  reasonForVisit?: string;
  symptomDescription?: string;
  severity?: string;
  onsetDuration?: string;
  bodyArea?: string;
  relevantMedicalHistory: string[];
  currentMedications: string[];
  allergies: string[];
}
```

2. **Create `IntakeApiService`** API client:

```typescript
// client/src/app/features/scheduling/services/intake.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  SaveDraftRequest,
  SaveDraftResponse,
  IntakeDraftResponse,
  SubmitIntakeRequest,
  SubmitIntakeResponse,
  IntakeAssistRequest,
  IntakeAssistResponse,
} from '../models/intake.model';

@Injectable({ providedIn: 'root' })
export class IntakeApiService {
  private readonly baseUrl = '/api/v1/intake';

  constructor(private http: HttpClient) {}

  saveDraft(request: SaveDraftRequest): Observable<SaveDraftResponse> {
    return this.http.put<SaveDraftResponse>(
      `${this.baseUrl}/draft`,
      request
    );
  }

  getDraft(slotId?: string): Observable<IntakeDraftResponse> {
    const params = slotId ? { slotId } : {};
    return this.http.get<IntakeDraftResponse>(`${this.baseUrl}/draft`, {
      params,
    });
  }

  submitIntake(
    request: SubmitIntakeRequest
  ): Observable<SubmitIntakeResponse> {
    return this.http.post<SubmitIntakeResponse>(
      `${this.baseUrl}/submit`,
      request
    );
  }

  aiAssist(
    request: IntakeAssistRequest
  ): Observable<IntakeAssistResponse> {
    return this.http.post<IntakeAssistResponse>(
      `${this.baseUrl}/ai-assist`,
      request
    );
  }
}
```

3. **Create `AiAssistPanelComponent`** for the AI-mode free-text input:

```typescript
// client/src/app/features/scheduling/components/ai-assist-panel/ai-assist-panel.component.ts
import {
  Component,
  ChangeDetectionStrategy,
  signal,
  output,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-ai-assist-panel',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './ai-assist-panel.component.html',
  styleUrls: ['./ai-assist-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiAssistPanelComponent {
  freeText = signal('');
  isProcessing = signal(false);

  submitted = output<string>();

  onSubmit(): void {
    const text = this.freeText().trim();
    if (!text || this.isProcessing()) return;
    this.submitted.emit(text);
  }

  setProcessing(value: boolean): void {
    this.isProcessing.set(value);
  }
}
```

```html
<!-- ai-assist-panel.component.html -->
<div class="ai-assist-panel" role="region" aria-label="AI-assisted intake">
  <div class="panel-header">
    <mat-icon class="ai-icon">auto_awesome</mat-icon>
    <span>Describe your symptoms in your own words</span>
  </div>

  <mat-form-field appearance="outline" class="full-width">
    <mat-label>Your symptoms</mat-label>
    <textarea
      matInput
      [(ngModel)]="freeText"
      rows="4"
      placeholder="Example: I have been having headaches for the past 3 days, mostly on the right side..."
      [disabled]="isProcessing()"
      aria-describedby="ai-assist-hint">
    </textarea>
    <mat-hint id="ai-assist-hint">
      The AI will suggest structured fields from your description
    </mat-hint>
  </mat-form-field>

  <button
    mat-flat-button
    color="primary"
    (click)="onSubmit()"
    [disabled]="!freeText().trim() || isProcessing()"
    aria-label="Generate intake suggestions from description">
    @if (isProcessing()) {
      <mat-spinner diameter="20" />
      <span>Analyzing...</span>
    } @else {
      <mat-icon>auto_awesome</mat-icon>
      <span>Generate Suggestions</span>
    }
  </button>
</div>
```

```scss
// ai-assist-panel.component.scss
.ai-assist-panel {
  padding: 16px;
  border-radius: 12px;
  background: var(--mat-sys-tertiary-container, #e8eaf6);
  border: 1px solid var(--mat-sys-outline-variant, #c5cae9);
  margin-bottom: 24px;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  font-weight: 500;
  color: var(--mat-sys-on-tertiary-container, #1a237e);

  .ai-icon {
    color: var(--mat-sys-tertiary, #5c6bc0);
  }
}

.full-width {
  width: 100%;
}

button {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;

  mat-spinner {
    display: inline-block;
  }
}
```

4. **Create `IntakeFormComponent`** — the main page implementing SCR-005 with all five states:

```typescript
// client/src/app/features/scheduling/pages/intake-form/intake-form.component.ts
import {
  Component,
  ChangeDetectionStrategy,
  signal,
  computed,
  OnInit,
  OnDestroy,
  ViewChild,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, debounceTime, takeUntil, merge } from 'rxjs';
import { IntakeApiService } from '../../services/intake.service';
import { AiAssistPanelComponent } from '../../components/ai-assist-panel/ai-assist-panel.component';
import { IntakeAssistResponse } from '../../models/intake.model';

type FormState = 'loading' | 'empty' | 'default' | 'error' | 'submitting';

@Component({
  selector: 'app-intake-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatSnackBarModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    AiAssistPanelComponent,
    DatePipe,
  ],
  templateUrl: './intake-form.component.html',
  styleUrls: ['./intake-form.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IntakeFormComponent implements OnInit, OnDestroy {
  @ViewChild(AiAssistPanelComponent)
  aiPanel?: AiAssistPanelComponent;

  private destroy$ = new Subject<void>();
  private autosave$ = new Subject<void>();

  formState = signal<FormState>('loading');
  aiMode = signal(false);
  aiPopulatedFields = signal<string[]>([]);
  autosaveStatus = signal<{ saved: boolean; timestamp?: Date }>({
    saved: false,
  });
  draftId = signal<string | null>(null);
  isSubmitting = signal(false);

  slotId: string | null = null;
  appointmentId: string | null = null;

  // Section completion tracking for progress bar (Validation state)
  sectionProgress = computed(() => {
    const form = this.intakeForm;
    let completed = 0;
    let total = 4;

    if (form.get('personalInfo')?.valid) completed++;
    if (form.get('reasonForVisit')?.valid) completed++;
    if (form.get('medicalHistory')?.valid) completed++;
    if (form.get('insurance')?.valid) completed++;

    return { completed, total, percentage: (completed / total) * 100 };
  });

  intakeForm = new FormGroup({
    personalInfo: new FormGroup({
      firstName: new FormControl('', [Validators.required]),
      lastName: new FormControl('', [Validators.required]),
      dateOfBirth: new FormControl('', [Validators.required]),
      phone: new FormControl('', [Validators.required]),
      email: new FormControl('', [Validators.email]),
    }),
    reasonForVisit: new FormGroup({
      chiefComplaint: new FormControl('', [Validators.required]),
      symptomDescription: new FormControl(''),
      severity: new FormControl(''),
      onsetDuration: new FormControl(''),
      bodyArea: new FormControl(''),
    }),
    medicalHistory: new FormGroup({
      conditions: new FormControl(''),
      medications: new FormControl(''),
      allergies: new FormControl(''),
      surgeries: new FormControl(''),
    }),
    insurance: new FormGroup({
      provider: new FormControl(''),
      memberId: new FormControl(''),
      groupNumber: new FormControl(''),
    }),
  });

  severityOptions = ['Mild', 'Moderate', 'Severe'];

  constructor(
    private intakeApi: IntakeApiService,
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.slotId = this.route.snapshot.queryParamMap.get('slotId');
    this.appointmentId =
      this.route.snapshot.queryParamMap.get('appointmentId');

    // Load existing draft (AC-3)
    this.loadDraft();

    // Autosave on blur — debounced 500ms (AC-2)
    this.autosave$
      .pipe(debounceTime(500), takeUntil(this.destroy$))
      .subscribe(() => this.saveDraft());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // AC-3: Load saved draft on page entry
  private loadDraft(): void {
    this.formState.set('loading');

    this.intakeApi.getDraft(this.slotId ?? undefined).subscribe({
      next: (draft) => {
        if (draft && draft.formData) {
          this.intakeForm.patchValue(draft.formData as any);
          this.draftId.set(draft.id);
          this.aiPopulatedFields.set(draft.aiPopulatedFields);
          this.formState.set('default');
        } else {
          this.formState.set('empty');
        }
      },
      error: (err) => {
        if (err.status === 204) {
          this.formState.set('empty');
        } else {
          this.formState.set('error');
        }
      },
    });
  }

  // AC-2: Autosave triggered on blur
  onFieldBlur(): void {
    this.autosave$.next();
  }

  private saveDraft(): void {
    const formData = this.intakeForm.getRawValue();

    this.intakeApi
      .saveDraft({
        slotId: this.slotId ?? undefined,
        formData,
        aiPopulatedFields: this.aiPopulatedFields(),
      })
      .subscribe({
        next: (result) => {
          this.draftId.set(result.draftId);
          this.autosaveStatus.set({
            saved: true,
            timestamp: new Date(result.savedAt),
          });

          if (this.formState() === 'empty') {
            this.formState.set('default');
          }
        },
        error: () => {
          // Autosave failure — show toast (Error state)
          this.snackBar.open(
            'Autosave failed. Your changes may not be saved.',
            'Dismiss',
            { duration: 5000, panelClass: 'error-snackbar' }
          );
        },
      });
  }

  // AC-1: AI-assist toggle and suggestion handling
  onAiToggle(enabled: boolean): void {
    this.aiMode.set(enabled);
  }

  onAiSubmit(freeText: string): void {
    this.aiPanel?.setProcessing(true);

    this.intakeApi.aiAssist({ freeTextDescription: freeText }).subscribe({
      next: (response: IntakeAssistResponse) => {
        this.aiPanel?.setProcessing(false);

        if (response.aiAssisted && response.suggestions) {
          // Pre-populate form fields from AI suggestions
          const s = response.suggestions;
          this.intakeForm.patchValue({
            reasonForVisit: {
              chiefComplaint: s.reasonForVisit ?? '',
              symptomDescription: s.symptomDescription ?? '',
              severity: s.severity ?? '',
              onsetDuration: s.onsetDuration ?? '',
              bodyArea: s.bodyArea ?? '',
            },
            medicalHistory: {
              conditions:
                s.relevantMedicalHistory?.join(', ') ?? '',
              medications:
                s.currentMedications?.join(', ') ?? '',
              allergies: s.allergies?.join(', ') ?? '',
            },
          });

          this.aiPopulatedFields.set(response.aiPopulatedFields);

          this.snackBar.open(
            'AI suggestions applied. Review and edit as needed.',
            'OK',
            { duration: 4000 }
          );

          // Trigger autosave after AI population
          this.autosave$.next();
        } else {
          // Fallback notification (edge case: AI failure)
          this.aiMode.set(false);
          this.snackBar.open(
            response.fallbackReason
              ?? 'AI assist unavailable, please fill in manually.',
            'OK',
            { duration: 5000, panelClass: 'warning-snackbar' }
          );
        }
      },
      error: () => {
        this.aiPanel?.setProcessing(false);
        this.aiMode.set(false);
        this.snackBar.open(
          'AI assist unavailable, please fill in manually.',
          'OK',
          { duration: 5000, panelClass: 'warning-snackbar' }
        );
      },
    });
  }

  // AC-4: Submit intake
  onSubmit(): void {
    if (this.intakeForm.invalid || this.isSubmitting()) return;

    this.intakeForm.markAllAsTouched();

    if (this.intakeForm.invalid) return;

    this.isSubmitting.set(true);
    this.formState.set('submitting');

    this.intakeApi
      .submitIntake({
        draftId: this.draftId()!,
        appointmentId: this.appointmentId!,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.router.navigate(['/scheduling/confirmation'], {
            queryParams: {
              appointmentId: this.appointmentId,
            },
          });
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.formState.set('default');

          const message =
            err.error?.title ?? 'Submission failed. Please try again.';
          this.snackBar.open(message, 'Dismiss', {
            duration: 5000,
            panelClass: 'error-snackbar',
          });
        },
      });
  }

  onBack(): void {
    // Autosave before navigating back
    this.saveDraft();
    this.router.navigate(['/scheduling/search']);
  }

  isAiPopulated(fieldName: string): boolean {
    return this.aiPopulatedFields().includes(fieldName);
  }

  onRetry(): void {
    this.loadDraft();
  }
}
```

5. **Create the intake form template** implementing all SCR-005 states:

```html
<!-- intake-form.component.html -->
<div class="intake-form-page">
  <!-- Progress Bar (Validation state) -->
  @if (formState() === 'default' || formState() === 'submitting') {
    <div class="progress-section" role="progressbar"
         [attr.aria-valuenow]="sectionProgress().percentage"
         aria-valuemin="0" aria-valuemax="100"
         [attr.aria-label]="sectionProgress().completed + ' of ' + sectionProgress().total + ' sections complete'">
      <mat-progress-bar
        mode="determinate"
        [value]="sectionProgress().percentage"
        [class.complete]="sectionProgress().percentage === 100" />
      <span class="progress-label">
        {{ sectionProgress().completed }}/{{ sectionProgress().total }} sections complete
      </span>
    </div>
  }

  <header class="page-header">
    <h1>Patient Intake Form</h1>
    <!-- AI-Assist Toggle (UXR-104) -->
    <div class="ai-toggle">
      <mat-slide-toggle
        [checked]="aiMode()"
        (change)="onAiToggle($event.checked)"
        aria-label="Toggle AI-assisted intake mode">
        <mat-icon>auto_awesome</mat-icon>
        AI-Assisted
      </mat-slide-toggle>
    </div>
  </header>

  <!-- Loading State -->
  @if (formState() === 'loading') {
    <mat-progress-bar mode="indeterminate" />
    <div class="skeleton-form" aria-busy="true" aria-label="Loading intake form">
      @for (i of [1, 2, 3, 4]; track i) {
        <div class="skeleton-section">
          <div class="skeleton-line wide"></div>
          <div class="skeleton-line medium"></div>
          <div class="skeleton-line medium"></div>
        </div>
      }
    </div>
  }

  <!-- Error State -->
  @if (formState() === 'error') {
    <div class="error-state" role="alert">
      <mat-icon>cloud_off</mat-icon>
      <p>Unable to load intake form. Please try again.</p>
      <button mat-stroked-button (click)="onRetry()">
        <mat-icon>refresh</mat-icon> Retry
      </button>
    </div>
  }

  <!-- AI Assist Panel (visible in AI mode) -->
  @if (aiMode() && (formState() === 'default' || formState() === 'empty')) {
    <app-ai-assist-panel (submitted)="onAiSubmit($event)" />
  }

  <!-- Main Form (Default / Empty / Validation states) -->
  @if (formState() === 'default' || formState() === 'empty' || formState() === 'submitting') {
    <form [formGroup]="intakeForm" (ngSubmit)="onSubmit()" novalidate>

      <!-- Section 1: Personal Information -->
      <section class="form-section" formGroupName="personalInfo">
        <h2>
          Personal Information
          @if (intakeForm.get('personalInfo')?.valid) {
            <mat-icon class="section-check">check_circle</mat-icon>
          }
        </h2>

        <mat-form-field appearance="outline">
          <mat-label>First Name</mat-label>
          <input matInput formControlName="firstName" (blur)="onFieldBlur()"
                 [attr.aria-describedby]="intakeForm.get('personalInfo.firstName')?.hasError('required') ? 'firstName-error' : null" />
          @if (intakeForm.get('personalInfo.firstName')?.hasError('required')
               && intakeForm.get('personalInfo.firstName')?.touched) {
            <mat-error id="firstName-error">
              <mat-icon>error</mat-icon> First name is required
            </mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Last Name</mat-label>
          <input matInput formControlName="lastName" (blur)="onFieldBlur()"
                 [attr.aria-describedby]="intakeForm.get('personalInfo.lastName')?.hasError('required') ? 'lastName-error' : null" />
          @if (intakeForm.get('personalInfo.lastName')?.hasError('required')
               && intakeForm.get('personalInfo.lastName')?.touched) {
            <mat-error id="lastName-error">
              <mat-icon>error</mat-icon> Last name is required
            </mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Date of Birth</mat-label>
          <input matInput type="date" formControlName="dateOfBirth" (blur)="onFieldBlur()" />
          @if (intakeForm.get('personalInfo.dateOfBirth')?.hasError('required')
               && intakeForm.get('personalInfo.dateOfBirth')?.touched) {
            <mat-error><mat-icon>error</mat-icon> Date of birth is required</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Phone</mat-label>
          <input matInput type="tel" formControlName="phone" (blur)="onFieldBlur()" />
          @if (intakeForm.get('personalInfo.phone')?.hasError('required')
               && intakeForm.get('personalInfo.phone')?.touched) {
            <mat-error><mat-icon>error</mat-icon> Phone number is required</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Email (optional)</mat-label>
          <input matInput type="email" formControlName="email" (blur)="onFieldBlur()" />
          @if (intakeForm.get('personalInfo.email')?.hasError('email')) {
            <mat-error><mat-icon>error</mat-icon> Invalid email format</mat-error>
          }
        </mat-form-field>
      </section>

      <!-- Section 2: Reason for Visit -->
      <section class="form-section" formGroupName="reasonForVisit">
        <h2>
          Reason for Visit
          @if (intakeForm.get('reasonForVisit')?.valid) {
            <mat-icon class="section-check">check_circle</mat-icon>
          }
        </h2>

        <mat-form-field appearance="outline">
          <mat-label>Chief Complaint</mat-label>
          <input matInput formControlName="chiefComplaint" (blur)="onFieldBlur()"
                 [class.ai-filled]="isAiPopulated('reasonForVisit')" />
          @if (isAiPopulated('reasonForVisit')) {
            <span class="ai-badge" matSuffix>
              <mat-icon>auto_awesome</mat-icon> AI
            </span>
          }
          @if (intakeForm.get('reasonForVisit.chiefComplaint')?.hasError('required')
               && intakeForm.get('reasonForVisit.chiefComplaint')?.touched) {
            <mat-error><mat-icon>error</mat-icon> Chief complaint is required</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Symptom Description</mat-label>
          <textarea matInput formControlName="symptomDescription" rows="3"
                    (blur)="onFieldBlur()"
                    [class.ai-filled]="isAiPopulated('symptomDescription')"></textarea>
          @if (isAiPopulated('symptomDescription')) {
            <span class="ai-badge" matSuffix>
              <mat-icon>auto_awesome</mat-icon> AI
            </span>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Severity</mat-label>
          <mat-select formControlName="severity" (selectionChange)="onFieldBlur()"
                      [class.ai-filled]="isAiPopulated('severity')">
            <mat-option [value]="''">Not specified</mat-option>
            @for (s of severityOptions; track s) {
              <mat-option [value]="s">{{ s }}</mat-option>
            }
          </mat-select>
          @if (isAiPopulated('severity')) {
            <span class="ai-badge" matSuffix>
              <mat-icon>auto_awesome</mat-icon> AI
            </span>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Onset / Duration</mat-label>
          <input matInput formControlName="onsetDuration" (blur)="onFieldBlur()"
                 [class.ai-filled]="isAiPopulated('onsetDuration')" />
          @if (isAiPopulated('onsetDuration')) {
            <span class="ai-badge" matSuffix>
              <mat-icon>auto_awesome</mat-icon> AI
            </span>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Body Area</mat-label>
          <input matInput formControlName="bodyArea" (blur)="onFieldBlur()" />
        </mat-form-field>
      </section>

      <!-- Section 3: Medical History -->
      <section class="form-section" formGroupName="medicalHistory">
        <h2>
          Medical History
          @if (intakeForm.get('medicalHistory')?.valid) {
            <mat-icon class="section-check">check_circle</mat-icon>
          }
        </h2>

        <mat-form-field appearance="outline">
          <mat-label>Existing Conditions</mat-label>
          <textarea matInput formControlName="conditions" rows="2"
                    (blur)="onFieldBlur()"
                    [class.ai-filled]="isAiPopulated('relevantMedicalHistory')"></textarea>
          @if (isAiPopulated('relevantMedicalHistory')) {
            <span class="ai-badge" matSuffix>
              <mat-icon>auto_awesome</mat-icon> AI
            </span>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Current Medications</mat-label>
          <textarea matInput formControlName="medications" rows="2"
                    (blur)="onFieldBlur()"
                    [class.ai-filled]="isAiPopulated('currentMedications')"></textarea>
          @if (isAiPopulated('currentMedications')) {
            <span class="ai-badge" matSuffix>
              <mat-icon>auto_awesome</mat-icon> AI
            </span>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Allergies</mat-label>
          <textarea matInput formControlName="allergies" rows="2"
                    (blur)="onFieldBlur()"
                    [class.ai-filled]="isAiPopulated('allergies')"></textarea>
          @if (isAiPopulated('allergies')) {
            <span class="ai-badge" matSuffix>
              <mat-icon>auto_awesome</mat-icon> AI
            </span>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Previous Surgeries</mat-label>
          <textarea matInput formControlName="surgeries" rows="2"
                    (blur)="onFieldBlur()"></textarea>
        </mat-form-field>
      </section>

      <!-- Section 4: Insurance Reference -->
      <section class="form-section" formGroupName="insurance">
        <h2>
          Insurance Reference
          @if (intakeForm.get('insurance')?.valid) {
            <mat-icon class="section-check">check_circle</mat-icon>
          }
        </h2>

        <mat-form-field appearance="outline">
          <mat-label>Insurance Provider</mat-label>
          <input matInput formControlName="provider" (blur)="onFieldBlur()" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Member ID</mat-label>
          <input matInput formControlName="memberId" (blur)="onFieldBlur()" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Group Number</mat-label>
          <input matInput formControlName="groupNumber" (blur)="onFieldBlur()" />
        </mat-form-field>
      </section>
    </form>
  }

  <!-- Sticky Bottom Bar -->
  @if (formState() === 'default' || formState() === 'empty' || formState() === 'submitting') {
    <div class="sticky-footer">
      <!-- Autosave Indicator (AC-2) -->
      <div class="autosave-indicator" role="status" aria-live="polite">
        @if (autosaveStatus().saved) {
          <mat-icon class="saved-icon">cloud_done</mat-icon>
          <span>Saved {{ autosaveStatus().timestamp | date:'shortTime' }}</span>
        }
      </div>

      <div class="footer-actions">
        <button mat-stroked-button (click)="onBack()" type="button"
                [disabled]="isSubmitting()">
          Back
        </button>
        <button mat-flat-button color="primary" (click)="onSubmit()"
                [disabled]="intakeForm.invalid || isSubmitting()"
                aria-label="Submit intake form">
          @if (isSubmitting()) {
            <mat-spinner diameter="20" />
            <span>Submitting...</span>
          } @else {
            Submit
          }
        </button>
      </div>
    </div>
  }
</div>
```

6. **Create the intake form styles** with responsive layout and AI badges:

```scss
// intake-form.component.scss
.intake-form-page {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px 24px 100px; // Bottom padding for sticky footer
}

// Progress bar (Validation state)
.progress-section {
  margin-bottom: 16px;

  .progress-label {
    display: block;
    text-align: right;
    font-size: 0.75rem;
    color: var(--mat-sys-on-surface-variant, #555);
    margin-top: 4px;
  }

  mat-progress-bar.complete ::ng-deep .mdc-linear-progress__bar-inner {
    border-color: var(--mat-sys-tertiary, #4caf50);
  }
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;

  h1 {
    font-size: 1.5rem;
    font-weight: 600;
    margin: 0;
    color: var(--mat-sys-on-surface, #1a1a1a);
  }
}

.ai-toggle {
  display: flex;
  align-items: center;
  gap: 4px;

  mat-icon {
    font-size: 18px;
    width: 18px;
    height: 18px;
    vertical-align: middle;
    color: var(--mat-sys-tertiary, #5c6bc0);
  }
}

// Form sections
.form-section {
  margin-bottom: 32px;
  padding: 20px;
  border-radius: 12px;
  background: var(--mat-sys-surface, #fff);
  border: 1px solid var(--mat-sys-outline-variant, #e0e0e0);

  h2 {
    font-size: 1.125rem;
    font-weight: 600;
    margin: 0 0 16px;
    color: var(--mat-sys-on-surface, #333);
    display: flex;
    align-items: center;
    gap: 8px;

    .section-check {
      color: var(--mat-sys-tertiary, #4caf50);
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
  }

  mat-form-field {
    width: 100%;
    margin-bottom: 8px;
  }
}

// AI badge (UXR-405)
.ai-badge {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  padding: 2px 8px;
  border-radius: 12px;
  background: var(--mat-sys-tertiary-container, #e8eaf6);
  color: var(--mat-sys-on-tertiary-container, #1a237e);
  font-size: 0.6875rem;
  font-weight: 600;
  white-space: nowrap;

  mat-icon {
    font-size: 12px;
    width: 12px;
    height: 12px;
  }
}

.ai-filled {
  background: var(--mat-sys-tertiary-container, #e8eaf6) !important;
}

// Error state
.error-state {
  text-align: center;
  padding: 48px 24px;

  mat-icon {
    font-size: 48px;
    width: 48px;
    height: 48px;
    color: var(--mat-sys-error, #dc2626);
    margin-bottom: 16px;
  }

  p {
    color: var(--mat-sys-on-surface-variant, #555);
    margin: 0 0 16px;
  }
}

// Skeleton loading
.skeleton-form .skeleton-section {
  background: var(--mat-sys-surface, #fff);
  border-radius: 12px;
  padding: 20px;
  margin-bottom: 16px;
  border: 1px solid var(--mat-sys-outline-variant, #e0e0e0);
}

.skeleton-line {
  height: 14px;
  border-radius: 4px;
  background: linear-gradient(
    90deg,
    var(--mat-sys-surface-variant, #e0e0e0) 25%,
    var(--mat-sys-surface-container, #eee) 50%,
    var(--mat-sys-surface-variant, #e0e0e0) 75%
  );
  background-size: 200% 100%;
  animation: shimmer 1.5s infinite;
  margin-bottom: 12px;

  &.wide { width: 60%; }
  &.medium { width: 100%; height: 40px; }
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

// Sticky footer
.sticky-footer {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 24px;
  background: var(--mat-sys-surface, #fff);
  border-top: 1px solid var(--mat-sys-outline-variant, #e0e0e0);
  box-shadow: 0 -4px 12px rgba(0, 0, 0, 0.08);
  z-index: 10;
}

.autosave-indicator {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 0.8125rem;
  color: var(--mat-sys-on-surface-variant, #777);

  .saved-icon {
    color: var(--mat-sys-tertiary, #4caf50);
    font-size: 18px;
    width: 18px;
    height: 18px;
  }
}

.footer-actions {
  display: flex;
  gap: 12px;

  button {
    display: flex;
    align-items: center;
    gap: 6px;

    mat-spinner {
      display: inline-block;
    }
  }
}

// Inline validation errors (UXR-205, UXR-601)
mat-error {
  display: flex;
  align-items: center;
  gap: 4px;

  mat-icon {
    font-size: 14px;
    width: 14px;
    height: 14px;
  }
}

// Responsive (UXR-301)
@media (max-width: 767px) {
  .intake-form-page {
    padding: 16px 16px 100px;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .form-section {
    padding: 16px;
  }

  .sticky-footer {
    flex-direction: column;
    gap: 8px;
    padding: 12px 16px;

    .footer-actions {
      width: 100%;

      button {
        flex: 1;
      }
    }
  }
}
```

7. **Add intake route** to scheduling feature:

```typescript
// Add to scheduling-routing.module.ts
{
  path: 'intake',
  loadComponent: () =>
    import('./pages/intake-form/intake-form.component')
      .then(m => m.IntakeFormComponent),
  title: 'Patient Intake Form',
},
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts
            ├── features/
            │   ├── auth/
            │   └── scheduling/
            │       ├── pages/
            │       │   ├── slot-search/            (from US_019)
            │       │   └── intake-form/            (new page)
            │       ├── components/
            │       │   ├── slot-card/              (from US_019)
            │       │   └── ai-assist-panel/        (new component)
            │       ├── services/
            │       │   ├── slot-search.service.ts  (from US_019)
            │       │   └── intake.service.ts       (new service)
            │       ├── models/
            │       │   ├── slot.model.ts           (from US_019)
            │       │   └── intake.model.ts         (new models)
            │       └── scheduling-routing.module.ts
            └── shared/
```

> Placeholder: Update on execution based on US_019 and US_020 task_001/002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/scheduling/pages/intake-form/intake-form.component.ts | Main intake page with AI toggle, autosave, draft restore, all SCR-005 states |
| CREATE | client/src/app/features/scheduling/pages/intake-form/intake-form.component.html | Multi-section form, AI toggle header, progress bar, sticky footer |
| CREATE | client/src/app/features/scheduling/pages/intake-form/intake-form.component.scss | Single-column 720px layout, AI badges, autosave indicator, responsive |
| CREATE | client/src/app/features/scheduling/components/ai-assist-panel/ai-assist-panel.component.ts | AI-mode free-text textarea with submit trigger |
| CREATE | client/src/app/features/scheduling/components/ai-assist-panel/ai-assist-panel.component.html | AI panel template with processing spinner |
| CREATE | client/src/app/features/scheduling/components/ai-assist-panel/ai-assist-panel.component.scss | AI panel styles with tertiary color theme |
| CREATE | client/src/app/features/scheduling/services/intake.service.ts | HTTP client for draft save/retrieve/submit and AI-assist |
| CREATE | client/src/app/features/scheduling/models/intake.model.ts | TypeScript interfaces for all intake DTOs |
| MODIFY | client/src/app/features/scheduling/scheduling-routing.module.ts | Add lazy-loaded intake form route |

## External References

- Angular Material Slide Toggle: https://material.angular.io/components/slide-toggle/overview
- Angular Reactive Forms: https://angular.dev/guide/forms/reactive-forms
- RxJS debounceTime: https://rxjs.dev/api/operators/debounceTime
- WCAG 2.1 AA Error Identification: https://www.w3.org/WAI/WCAG21/Understanding/error-identification
- Angular Signals: https://angular.dev/guide/signals

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve frontend
ng serve

# Navigate to intake form (from slot search)
# http://localhost:4200/scheduling/intake?slotId=<guid>&appointmentId=<guid>
```

## Implementation Validation Strategy

- [ ] AI toggle in header switches between manual and AI-assisted modes (UXR-104)
- [ ] In AI mode, free-text textarea appears with "Generate Suggestions" button (AC-1)
- [ ] AI suggestions pre-populate form fields within 2.5 seconds of submission (AC-1)
- [ ] AI-populated fields display labeled badge distinguishing them from manual entry (UXR-405)
- [ ] AI failure reverts toggle to manual mode with snackbar notification (edge case)
- [ ] Blur event on any field triggers autosave via debounced PUT /draft (AC-2)
- [ ] "Saved" indicator with timestamp appears in sticky footer within 1 second (AC-2)
- [ ] Autosave failure shows warning toast but does not block form usage (SCR-005 Error)
- [ ] On page load, existing draft is restored from GET /draft?slotId (AC-3)
- [ ] Four form sections: personal info, reason for visit, medical history, insurance (SCR-005)
- [ ] Section completion checkmarks and green progress bar track completion (SCR-005 Validation)
- [ ] Required fields show inline errors with error icon on touch+invalid (UXR-205, UXR-601)
- [ ] Submit button shows loading spinner and disables during submission (UXR-501)
- [ ] Successful submission navigates to confirmation page with appointmentId (AC-4)
- [ ] Full keyboard navigation with visible focus indicators on all fields (UXR-202)
- [ ] Color contrast meets WCAG 2.1 AA (UXR-201)
- [ ] Responsive layout across 375px/768px/1440px breakpoints (UXR-301)

## Implementation Checklist

- [ ] Create TypeScript interfaces for all intake DTOs (draft, submit, AI-assist request/response)
- [ ] Create `IntakeApiService` with `saveDraft()`, `getDraft()`, `submitIntake()`, `aiAssist()` methods
- [ ] Create `AiAssistPanelComponent` with free-text textarea, processing spinner, and submit output
- [ ] Create `IntakeFormComponent` with four reactive form sections and AI toggle signal
- [ ] Implement autosave on blur with 500ms debounce and "Saved" indicator timestamp (AC-2)
- [ ] Implement draft restore on page load from `GET /draft?slotId` (AC-3)
- [ ] Implement AI-assist flow: toggle → free-text → API call → field population with AI badges (AC-1, UXR-405)
- [ ] Implement submit with validation, loading spinner, double-submit prevention, and navigation (AC-4, UXR-501)
