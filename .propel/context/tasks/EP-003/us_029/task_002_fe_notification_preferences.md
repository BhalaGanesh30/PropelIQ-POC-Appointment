# Task - TASK_002

## Requirement Reference

- User Story: us_029
- Story Location: .propel/context/tasks/EP-003/us_029/us_029.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as a patient, When I navigate to my notification preferences, Then I can toggle email and SMS channels on or off independently.
  - AC-2: Given I configure my preferences, When I save them, Then the changes are persisted and the next scheduled reminder is dispatched using my updated preference settings.
- Edge Cases:
  - What happens if a patient has no phone number on file but enables SMS? An inline prompt asks the patient to add a verified mobile number before SMS can be activated.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-009-notification-preferences.html` (pending) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-009 |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-501 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-013 but SCR-013 is "Document Viewer" (EP-006). The actual Notification Preferences screen is **SCR-009** per figma_spec.md, which describes "Patient-configurable notification channel (email, SMS) and reminder timing preferences."

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Frontend | Angular Material | 17.x |
| Frontend | RxJS | 7.x |
| Frontend | TypeScript | 5.x |
| Backend | N/A | N/A |
| Database | N/A | N/A |
| AI/ML | N/A | N/A |
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

Implement the notification preferences settings page (SCR-009) as an Angular standalone component. The page renders a single-column settings card (max-width 600px per SCR-009 layout spec) with mat-slide-toggle switches for email and SMS channel preferences (AC-1) and a mat-checkbox group for reminder timings (7d, 2d, 1d, 2h). The component loads current preferences from `GET /api/v1/patients/me/notification-preferences` on init and submits changes via `PUT` on save. The Save button shows a loading spinner and is disabled during the network request (UXR-501). On success, a mat-snackbar toast confirms "Preferences saved" (SCR-009 Validation state). On failure, an error toast with retry prompt is shown (SCR-009 Error state). When a patient toggles SMS on but has no phone number (`HasPhoneNumber = false` from API response), an inline alert prompts the patient to add a mobile number before the toggle takes effect (edge case 1) — the SMS toggle reverts to off and a link navigates to the profile page. All form controls support full keyboard navigation with visible focus indicators (UXR-202). The layout is responsive across mobile (375px), tablet (768px), and desktop (1440px) breakpoints (UXR-301). All text meets WCAG AA 4.5:1 contrast ratio (UXR-201). Default state pre-selects all channels and all timings for new patients (SCR-009 Empty state).

## Dependent Tasks

- US_029 task_001 (requires GET/PUT notification preferences API endpoints)

## Impacted Components

- New: `client/src/app/features/settings/notification-preferences.component.ts` (standalone component)
- New: `client/src/app/features/settings/notification-preferences.component.html` (template)
- New: `client/src/app/features/settings/notification-preferences.component.scss` (styles)
- New: `client/src/app/features/settings/notification-preferences-api.service.ts` (HttpClient service)
- New: `client/src/app/features/settings/models/notification-preference.models.ts` (TypeScript interfaces)
- Modify: `client/src/app/app.routes.ts` (add route for /settings/notifications)

## Implementation Plan

1. **Create TypeScript interfaces**:

```typescript
// client/src/app/features/settings/models/
//   notification-preference.models.ts

export interface NotificationPreferenceDto {
  emailEnabled: boolean;
  smsEnabled: boolean;
  reminderTimings: ReminderTiming[];
}

export interface NotificationPreferenceResponse
    extends NotificationPreferenceDto {
  hasPhoneNumber: boolean;
}

export type ReminderTiming = '7d' | '2d' | '1d' | '2h';

export const REMINDER_TIMING_LABELS:
    Record<ReminderTiming, string> = {
  '7d': '7 days before',
  '2d': '2 days before',
  '1d': '1 day before',
  '2h': '2 hours before'
};

export const ALL_TIMINGS: ReminderTiming[] =
  ['7d', '2d', '1d', '2h'];
```

2. **Create `NotificationPreferencesApiService`**:

```typescript
// client/src/app/features/settings/
//   notification-preferences-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  NotificationPreferenceDto,
  NotificationPreferenceResponse
} from './models/notification-preference.models';

@Injectable({ providedIn: 'root' })
export class NotificationPreferencesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl =
    '/api/v1/patients/me/notification-preferences';

  getPreferences():
      Observable<NotificationPreferenceResponse> {
    return this.http
      .get<NotificationPreferenceResponse>(this.baseUrl);
  }

  savePreferences(
    dto: NotificationPreferenceDto
  ): Observable<NotificationPreferenceResponse> {
    return this.http
      .put<NotificationPreferenceResponse>(
        this.baseUrl, dto);
  }
}
```

3. **Create `NotificationPreferencesComponent`** standalone:

```typescript
// client/src/app/features/settings/
//   notification-preferences.component.ts
import {
  Component, OnInit, signal, computed, inject
} from '@angular/core';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from
  '@angular/material/slide-toggle';
import { MatCheckboxModule } from
  '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from
  '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from
  '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import {
  NotificationPreferenceResponse,
  ReminderTiming,
  REMINDER_TIMING_LABELS,
  ALL_TIMINGS
} from './models/notification-preference.models';
import { NotificationPreferencesApiService } from
  './notification-preferences-api.service';

@Component({
  selector: 'app-notification-preferences',
  standalone: true,
  imports: [
    MatCardModule,
    MatSlideToggleModule,
    MatCheckboxModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatIconModule
  ],
  templateUrl:
    './notification-preferences.component.html',
  styleUrl:
    './notification-preferences.component.scss'
})
export class NotificationPreferencesComponent
    implements OnInit {
  private readonly api = inject(
    NotificationPreferencesApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  // State signals
  readonly emailEnabled = signal(true);
  readonly smsEnabled = signal(false);
  readonly reminderTimings =
    signal<ReminderTiming[]>([...ALL_TIMINGS]);
  readonly hasPhoneNumber = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly showPhonePrompt = signal(false);

  readonly timingLabels = REMINDER_TIMING_LABELS;
  readonly allTimings = ALL_TIMINGS;

  // UXR-501: Disable save during network request
  readonly saveDisabled = computed(() =>
    this.saving());

  ngOnInit(): void {
    this.loadPreferences();
  }

  private loadPreferences(): void {
    this.loading.set(true);
    this.api.getPreferences().subscribe({
      next: (prefs) => {
        this.emailEnabled.set(prefs.emailEnabled);
        this.smsEnabled.set(prefs.smsEnabled);
        this.reminderTimings.set(
          [...prefs.reminderTimings] as ReminderTiming[]);
        this.hasPhoneNumber.set(prefs.hasPhoneNumber);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open(
          'Failed to load preferences', 'Retry',
          { duration: 5000 });
      }
    });
  }

  // AC-1: Toggle SMS with phone validation
  onSmsToggle(enabled: boolean): void {
    if (enabled && !this.hasPhoneNumber()) {
      // Edge case 1: No phone number
      this.showPhonePrompt.set(true);
      this.smsEnabled.set(false);
      return;
    }
    this.showPhonePrompt.set(false);
    this.smsEnabled.set(enabled);
  }

  onEmailToggle(enabled: boolean): void {
    this.emailEnabled.set(enabled);
  }

  onTimingToggle(
    timing: ReminderTiming, checked: boolean
  ): void {
    const current = this.reminderTimings();
    if (checked) {
      this.reminderTimings.set([...current, timing]);
    } else {
      this.reminderTimings.set(
        current.filter(t => t !== timing));
    }
  }

  isTimingEnabled(timing: ReminderTiming): boolean {
    return this.reminderTimings().includes(timing);
  }

  navigateToProfile(): void {
    this.router.navigate(['/profile']);
  }

  // AC-2: Save preferences
  save(): void {
    this.saving.set(true);
    this.api.savePreferences({
      emailEnabled: this.emailEnabled(),
      smsEnabled: this.smsEnabled(),
      reminderTimings: this.reminderTimings()
    }).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.hasPhoneNumber.set(updated.hasPhoneNumber);
        // SCR-009 Validation state: success toast
        this.snackBar.open(
          'Preferences saved', 'Close',
          { duration: 3000 });
      },
      error: () => {
        this.saving.set(false);
        // SCR-009 Error state: error toast with retry
        this.snackBar.open(
          'Failed to save preferences', 'Retry',
          { duration: 5000 });
      }
    });
  }
}
```

4. **Create template**:

```html
<!-- notification-preferences.component.html -->

<div class="preferences-container">
  <mat-card class="preferences-card">
    <mat-card-header>
      <mat-card-title>
        Notification Preferences
      </mat-card-title>
      <mat-card-subtitle>
        Choose how you receive appointment reminders
      </mat-card-subtitle>
    </mat-card-header>

    <mat-card-content>
      @if (loading()) {
        <div class="loading-state"
             role="status"
             aria-label="Loading preferences">
          <mat-spinner diameter="40"></mat-spinner>
        </div>
      } @else {
        <!-- Channel Toggles -->
        <section aria-labelledby="channels-heading">
          <h3 id="channels-heading">
            Notification Channels
          </h3>

          <!-- AC-1: Email toggle -->
          <mat-slide-toggle
            [checked]="emailEnabled()"
            (change)="onEmailToggle($event.checked)"
            aria-label="Enable email notifications">
            Email
          </mat-slide-toggle>

          <!-- AC-1: SMS toggle -->
          <mat-slide-toggle
            [checked]="smsEnabled()"
            (change)="onSmsToggle($event.checked)"
            aria-label="Enable SMS notifications">
            SMS
          </mat-slide-toggle>

          <!-- Edge case 1: Phone number prompt -->
          @if (showPhonePrompt()) {
            <div class="phone-prompt"
                 role="alert"
                 aria-live="polite">
              <mat-icon>info</mat-icon>
              <span>
                A verified mobile number is required
                for SMS notifications.
                <a (click)="navigateToProfile()"
                   (keydown.enter)="navigateToProfile()"
                   tabindex="0"
                   role="link">
                  Add your phone number
                </a>
              </span>
            </div>
          }
        </section>

        <!-- Reminder Timings -->
        <section aria-labelledby="timings-heading">
          <h3 id="timings-heading">
            Reminder Timing
          </h3>

          @for (timing of allTimings;
                track timing) {
            <mat-checkbox
              [checked]="isTimingEnabled(timing)"
              (change)="onTimingToggle(
                timing, $event.checked)"
              [attr.aria-label]="
                'Remind me ' + timingLabels[timing]">
              {{ timingLabels[timing] }}
            </mat-checkbox>
          }
        </section>

        <!-- Save Button -->
        <!-- UXR-501: Spinner + disabled during request -->
        <div class="actions">
          <button mat-raised-button
                  color="primary"
                  (click)="save()"
                  [disabled]="saveDisabled()">
            @if (saving()) {
              <mat-spinner diameter="20"
                           class="button-spinner">
              </mat-spinner>
              Saving...
            } @else {
              Save Preferences
            }
          </button>
        </div>
      }
    </mat-card-content>
  </mat-card>
</div>
```

5. **Create styles**:

```scss
// notification-preferences.component.scss

// SCR-009 Layout: Single-column settings card
.preferences-container {
  display: flex;
  justify-content: center;
  padding: 24px 16px;
}

.preferences-card {
  max-width: 600px;
  width: 100%;
}

mat-card-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
  padding-top: 16px;
}

section {
  display: flex;
  flex-direction: column;
  gap: 12px;

  h3 {
    font-size: 16px;
    font-weight: 500;
    margin: 0;
  }
}

mat-slide-toggle,
mat-checkbox {
  display: block;
}

.phone-prompt {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 12px;
  background: #FFF3E0;
  border-radius: 4px;
  border-left: 4px solid #FF9800;
  font-size: 14px;

  mat-icon {
    color: #FF9800;
    flex-shrink: 0;
  }

  a {
    color: #1976D2;
    text-decoration: underline;
    cursor: pointer;

    // UXR-202: Visible focus indicator
    &:focus-visible {
      outline: 2px solid #1976D2;
      outline-offset: 2px;
      border-radius: 2px;
    }
  }
}

.actions {
  display: flex;
  justify-content: flex-end;
  padding-top: 8px;
}

.button-spinner {
  display: inline-block;
  margin-right: 8px;
}

.loading-state {
  display: flex;
  justify-content: center;
  padding: 48px 0;
}

// UXR-301: Responsive breakpoints
@media (max-width: 375px) {
  .preferences-container {
    padding: 16px 8px;
  }

  .preferences-card {
    max-width: 100%;
  }
}

@media (min-width: 376px) and (max-width: 768px) {
  .preferences-container {
    padding: 24px 16px;
  }
}
```

6. **Add route**:

```typescript
// In app.routes.ts, add:
{
  path: 'settings/notifications',
  loadComponent: () =>
    import(
      './features/settings/' +
      'notification-preferences.component'
    ).then(m => m.NotificationPreferencesComponent),
  canActivate: [authGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts                              (modify)
            └── features/
                └── settings/
                    ├── notification-preferences.component.ts   (new)
                    ├── notification-preferences.component.html (new)
                    ├── notification-preferences.component.scss (new)
                    ├── notification-preferences-api.service.ts (new)
                    └── models/
                        └── notification-preference.models.ts   (new)
```

> Placeholder: Update on execution based on US_029 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/settings/models/notification-preference.models.ts | TypeScript interfaces, timing labels, constants |
| CREATE | client/src/app/features/settings/notification-preferences-api.service.ts | HttpClient GET/PUT service |
| CREATE | client/src/app/features/settings/notification-preferences.component.ts | Standalone component with signals, channel toggles, timing checkboxes |
| CREATE | client/src/app/features/settings/notification-preferences.component.html | Template with mat-card, slide-toggles, checkboxes, save button |
| CREATE | client/src/app/features/settings/notification-preferences.component.scss | Single-column card layout, responsive breakpoints, phone prompt |
| MODIFY | client/src/app/app.routes.ts | Add lazy-loaded route for /settings/notifications |

## External References

- Angular Material Slide Toggle: https://material.angular.io/components/slide-toggle/overview
- Angular Material Checkbox: https://material.angular.io/components/checkbox/overview
- Angular Material Card: https://material.angular.io/components/card/overview
- Angular Material Snack Bar: https://material.angular.io/components/snack-bar/overview
- WCAG 2.1 AA Contrast: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Navigate to: http://localhost:4200/settings/notifications
# Verify toggle switches, timing checkboxes, save flow, and phone prompt
```

## Implementation Validation Strategy

- [ ] Email and SMS toggles work independently (AC-1)
- [ ] Save persists preferences and shows success toast (AC-2)
- [ ] Phone number prompt shown when enabling SMS without phone on file (edge case 1)
- [ ] Save button shows spinner and is disabled during request (UXR-501)
- [ ] Error toast with retry shown on save failure (SCR-009 Error state)
- [ ] All form controls keyboard navigable with visible focus indicators (UXR-202)
- [ ] Layout responsive at 375px, 768px, 1440px breakpoints (UXR-301)
- [ ] Text meets WCAG AA 4.5:1 contrast ratio (UXR-201)

## Implementation Checklist

- [ ] Create TypeScript interfaces and timing label constants
- [ ] Implement NotificationPreferencesApiService with GET/PUT methods
- [ ] Build NotificationPreferencesComponent with signal-based state management
- [ ] Implement channel toggle switches with SMS phone validation guard
- [ ] Implement reminder timing checkbox group
- [ ] Add save button with loading spinner and disabled state
- [ ] Add success and error toast notifications
- [ ] Add lazy-loaded route in app.routes.ts
