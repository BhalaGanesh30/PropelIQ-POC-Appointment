# Task - TASK_002

## Requirement Reference

- User Story: us_030
- Story Location: .propel/context/tasks/EP-003/us_030/us_030.md
- Acceptance Criteria:
  - AC-2: Given I receive the preferred-slot alert, When I open the claim link, Then I see the slot details and a visible countdown timer showing remaining time (2 hours) with urgency color shift at 30 minutes remaining.
  - AC-4: Given the 2-hour claim window expires, When I attempt to claim after expiry, Then the link is invalidated and I am informed the slot was offered to another patient.
- Edge Cases:
  - How does the system handle timezone differences for the claim countdown? Countdown is always shown in the patient's browser timezone; the expiry timestamp is stored in UTC and converted client-side.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-008-waitlist-view.html` (pending) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-008 |
| **UXR Requirements** | UXR-112, UXR-201, UXR-203, UXR-301, UXR-404 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-014 but SCR-014 is "360-Degree Patient Profile" (EP-006). The actual Waitlist View with claim countdown timers is **SCR-008** per figma_spec.md, which describes "View active waitlist entries with preferred slot criteria and claim countdown timers for offered slots."

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

Implement the preferred-slot claim page and countdown timer components for the waitlist view (SCR-008). When a patient opens the HMAC-signed claim link from their email or SMS, the `SlotClaimPageComponent` resolves the token via `GET /api/v1/waitlist/claim-details?token=...` to retrieve slot details (date, time, type, provider, duration) and the UTC expiry timestamp. A `ClaimCountdownComponent` renders a live countdown timer (AC-2) calculated from the UTC expiry timestamp converted to the patient's browser timezone (edge case 2). The countdown uses `requestAnimationFrame` for smooth rendering and updates every second. Color-coded urgency follows SCR-008 layout spec and UXR-112/UXR-404: green when > 1 hour remaining, amber at 30 min–1 hour, red at < 30 minutes. At exactly 0, the timer displays "Expired" and disables the claim button. A "Claim Appointment" button triggers `POST /api/v1/waitlist/{id}/claim` with the HMAC token. On success, a confirmation card replaces the claim form with booking details and a link to the dashboard. On 410 Gone (AC-4), an expired page states "This slot has been offered to another patient" with a return-to-waitlist link. On 409 Conflict (concurrent claim), a similar message is shown. The countdown has `role="timer"` and `aria-live="polite"` with periodic screen reader announcements at 1 hour, 30 minutes, and 5 minutes (UXR-203). The page is responsive across 375px, 768px, and 1440px (UXR-301) with WCAG AA contrast compliance (UXR-201). The existing `WaitlistViewComponent` (US_023 task_002) is enhanced to show the countdown timer inline for offered entries.

## Dependent Tasks

- US_030 task_001 (requires claim endpoint with HMAC validation, SlotAlertPayload, GET claim-details endpoint)
- US_023 task_002 (requires WaitlistViewComponent for inline countdown integration)

## Impacted Components

- New: `client/src/app/features/waitlist/slot-claim-page.component.ts` (claim page with slot details)
- New: `client/src/app/features/waitlist/slot-claim-page.component.html` (template)
- New: `client/src/app/features/waitlist/slot-claim-page.component.scss` (styles)
- New: `client/src/app/features/waitlist/claim-countdown.component.ts` (reusable countdown timer)
- New: `client/src/app/features/waitlist/claim-countdown.component.html` (timer template)
- New: `client/src/app/features/waitlist/claim-countdown.component.scss` (urgency color styles)
- New: `client/src/app/features/waitlist/models/slot-claim.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/waitlist/slot-claim-api.service.ts` (HttpClient service)
- Modify: `client/src/app/features/waitlist/waitlist-view.component.ts` (add inline countdown for offered entries)
- Modify: `client/src/app/features/waitlist/waitlist-view.component.html` (render countdown in offered cards)
- Modify: `client/src/app/app.routes.ts` (add /claim route)

## Implementation Plan

1. **Create TypeScript interfaces**:

```typescript
// client/src/app/features/waitlist/models/
//   slot-claim.models.ts

export interface SlotClaimDetails {
  waitlistEntryId: string;
  slotDateTime: string;       // ISO 8601 UTC
  slotType: string;
  providerName: string;
  durationMinutes: number;
  expiresAtUtc: string;       // ISO 8601 UTC
  status: 'Offered' | 'Claimed' | 'Expired';
}

export interface ClaimResult {
  message: string;
}

export type CountdownUrgency =
  'normal' | 'warning' | 'critical' | 'expired';

// SCR-008 + UXR-404: Color semantics
export const URGENCY_COLORS:
    Record<CountdownUrgency, string> = {
  normal: '#4CAF50',    // Green: > 1h
  warning: '#FF9800',   // Amber: 30m–1h
  critical: '#F44336',  // Red: < 30m
  expired: '#9E9E9E'    // Grey: 0
};
```

2. **Create `SlotClaimApiService`**:

```typescript
// client/src/app/features/waitlist/
//   slot-claim-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from
  '@angular/common/http';
import { Observable } from 'rxjs';
import { SlotClaimDetails, ClaimResult } from
  './models/slot-claim.models';

@Injectable({ providedIn: 'root' })
export class SlotClaimApiService {
  private readonly http = inject(HttpClient);

  getClaimDetails(
    token: string
  ): Observable<SlotClaimDetails> {
    const params = new HttpParams().set('token', token);
    return this.http
      .get<SlotClaimDetails>(
        '/api/v1/waitlist/claim-details',
        { params });
  }

  claimSlot(
    entryId: string,
    claimToken: string
  ): Observable<ClaimResult> {
    return this.http
      .post<ClaimResult>(
        `/api/v1/waitlist/${entryId}/claim`,
        { claimToken });
  }
}
```

3. **Create `ClaimCountdownComponent`** (reusable):

```typescript
// client/src/app/features/waitlist/
//   claim-countdown.component.ts
import {
  Component, input, signal, computed,
  OnInit, OnDestroy, inject, NgZone
} from '@angular/core';
import {
  CountdownUrgency, URGENCY_COLORS
} from './models/slot-claim.models';

@Component({
  selector: 'app-claim-countdown',
  standalone: true,
  templateUrl: './claim-countdown.component.html',
  styleUrl: './claim-countdown.component.scss'
})
export class ClaimCountdownComponent
    implements OnInit, OnDestroy {
  // Edge case 2: expiresAtUtc in ISO, convert client-side
  expiresAtUtc = input.required<string>();

  readonly remainingMs = signal(0);
  readonly expired = signal(false);
  private animFrameId = 0;
  private zone = inject(NgZone);

  // UXR-112: Urgency color shift at 30 minutes
  readonly urgency = computed<CountdownUrgency>(() => {
    const ms = this.remainingMs();
    if (ms <= 0) return 'expired';
    if (ms < 30 * 60 * 1000) return 'critical';
    if (ms < 60 * 60 * 1000) return 'warning';
    return 'normal';
  });

  readonly color = computed(() =>
    URGENCY_COLORS[this.urgency()]);

  readonly display = computed(() => {
    const ms = this.remainingMs();
    if (ms <= 0) return 'Expired';
    const h = Math.floor(ms / 3_600_000);
    const m = Math.floor((ms % 3_600_000) / 60_000);
    const s = Math.floor((ms % 60_000) / 1_000);
    return `${h}h ${String(m).padStart(2, '0')}m ` +
      `${String(s).padStart(2, '0')}s`;
  });

  // UXR-203: Screen reader announcement text
  readonly ariaAnnouncement = computed(() => {
    const ms = this.remainingMs();
    if (ms <= 0) return 'Claim window has expired';
    const m = Math.floor(ms / 60_000);
    return `${m} minutes remaining to claim`;
  });

  ngOnInit(): void {
    // Run outside zone for performance
    this.zone.runOutsideAngular(() => this.tick());
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.animFrameId);
  }

  private tick(): void {
    const expiresAt = new Date(
      this.expiresAtUtc()).getTime();
    const now = Date.now();
    const remaining = Math.max(0, expiresAt - now);

    this.zone.run(() => {
      this.remainingMs.set(remaining);
      if (remaining <= 0) {
        this.expired.set(true);
        return;
      }
    });

    if (remaining > 0) {
      this.animFrameId =
        requestAnimationFrame(() => this.tick());
    }
  }
}
```

```html
<!-- claim-countdown.component.html -->
<!-- UXR-112: Visible countdown with urgency colors -->
<div class="countdown"
     [style.color]="color()"
     [class]="urgency()"
     role="timer"
     [attr.aria-label]="ariaAnnouncement()">
  <span class="countdown-display">
    {{ display() }}
  </span>
</div>

<!-- UXR-203: Periodic SR announcements -->
<div class="sr-only"
     aria-live="polite"
     aria-atomic="true">
  {{ ariaAnnouncement() }}
</div>
```

```scss
// claim-countdown.component.scss
.countdown {
  font-size: 28px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  text-align: center;
  padding: 16px;
  border-radius: 8px;
  transition: color 0.3s ease;

  &.normal {
    background: rgba(76, 175, 80, 0.1);
  }
  &.warning {
    background: rgba(255, 152, 0, 0.1);
  }
  &.critical {
    background: rgba(244, 67, 54, 0.1);
    animation: pulse 1s ease-in-out infinite;
  }
  &.expired {
    background: rgba(158, 158, 158, 0.1);
  }
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
```

4. **Create `SlotClaimPageComponent`**:

```typescript
// client/src/app/features/waitlist/
//   slot-claim-page.component.ts
import {
  Component, OnInit, signal, computed, inject
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from
  '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { DatePipe } from '@angular/common';
import { ClaimCountdownComponent } from
  './claim-countdown.component';
import { SlotClaimApiService } from
  './slot-claim-api.service';
import { SlotClaimDetails } from
  './models/slot-claim.models';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-slot-claim-page',
  standalone: true,
  imports: [
    MatCardModule, MatButtonModule,
    MatProgressSpinnerModule, MatIconModule,
    DatePipe, ClaimCountdownComponent
  ],
  templateUrl: './slot-claim-page.component.html',
  styleUrl: './slot-claim-page.component.scss'
})
export class SlotClaimPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(SlotClaimApiService);

  readonly slot = signal<SlotClaimDetails | null>(null);
  readonly loading = signal(true);
  readonly claiming = signal(false);
  readonly claimed = signal(false);
  readonly expired = signal(false);
  readonly errorMessage = signal('');
  private token = '';

  ngOnInit(): void {
    this.token = this.route.snapshot
      .queryParamMap.get('token') ?? '';

    if (!this.token) {
      this.errorMessage.set('Invalid claim link');
      this.loading.set(false);
      return;
    }

    this.api.getClaimDetails(this.token).subscribe({
      next: (details) => {
        this.slot.set(details);
        if (details.status === 'Expired') {
          this.expired.set(true);
        }
        if (details.status === 'Claimed') {
          this.claimed.set(true);
        }
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 410) {
          this.expired.set(true);
          this.errorMessage.set(
            'This slot has been offered to ' +
            'another patient.');
        } else {
          this.errorMessage.set(
            'Unable to load claim details');
        }
        this.loading.set(false);
      }
    });
  }

  // AC-3: Claim appointment
  claim(): void {
    const s = this.slot();
    if (!s) return;

    this.claiming.set(true);
    this.api.claimSlot(
      s.waitlistEntryId, this.token
    ).subscribe({
      next: () => {
        this.claimed.set(true);
        this.claiming.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.claiming.set(false);
        if (err.status === 410) {
          // AC-4: Expired
          this.expired.set(true);
          this.errorMessage.set(
            'This offer has expired. The slot was ' +
            'offered to another patient.');
        } else if (err.status === 409) {
          this.errorMessage.set(
            'This slot was claimed by ' +
            'another patient.');
        } else {
          this.errorMessage.set(
            'Failed to claim. Please try again.');
        }
      }
    });
  }

  onCountdownExpired(): void {
    this.expired.set(true);
  }

  goToWaitlist(): void {
    this.router.navigate(['/waitlist']);
  }

  goToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }
}
```

```html
<!-- slot-claim-page.component.html -->
<div class="claim-container">
  @if (loading()) {
    <mat-card class="claim-card">
      <mat-card-content>
        <div class="loading-state"
             role="status"
             aria-label="Loading claim details">
          <mat-spinner diameter="40"></mat-spinner>
        </div>
      </mat-card-content>
    </mat-card>
  } @else if (claimed()) {
    <!-- AC-3: Success confirmation -->
    <mat-card class="claim-card success-card">
      <mat-card-content>
        <mat-icon class="success-icon">
          check_circle
        </mat-icon>
        <h2>Appointment Claimed!</h2>
        @if (slot(); as s) {
          <p>Your appointment on
            {{ s.slotDateTime | date:'fullDate' }}
            at {{ s.slotDateTime | date:'shortTime' }}
            has been confirmed.</p>
        }
        <button mat-raised-button
                color="primary"
                (click)="goToDashboard()">
          View on Dashboard
        </button>
      </mat-card-content>
    </mat-card>
  } @else if (expired() || errorMessage()) {
    <!-- AC-4: Expired / error state -->
    <mat-card class="claim-card expired-card">
      <mat-card-content>
        <mat-icon class="expired-icon">
          timer_off
        </mat-icon>
        <h2>Slot No Longer Available</h2>
        <p>{{ errorMessage() ||
          'This offer has expired.' }}</p>
        <button mat-raised-button
                color="primary"
                (click)="goToWaitlist()">
          Return to Waitlist
        </button>
      </mat-card-content>
    </mat-card>
  } @else if (slot(); as s) {
    <!-- AC-2: Claim page with countdown -->
    <mat-card class="claim-card">
      <mat-card-header>
        <mat-card-title>
          Preferred Slot Available
        </mat-card-title>
      </mat-card-header>

      <mat-card-content>
        <!-- AC-2: Countdown timer -->
        <app-claim-countdown
          [expiresAtUtc]="s.expiresAtUtc">
        </app-claim-countdown>

        <!-- Slot details -->
        <div class="slot-details">
          <div class="detail-row">
            <span class="label">Date</span>
            <span class="value">
              {{ s.slotDateTime | date:'fullDate' }}
            </span>
          </div>
          <div class="detail-row">
            <span class="label">Time</span>
            <span class="value">
              {{ s.slotDateTime | date:'shortTime' }}
            </span>
          </div>
          <div class="detail-row">
            <span class="label">Type</span>
            <span class="value">{{ s.slotType }}</span>
          </div>
          <div class="detail-row">
            <span class="label">Provider</span>
            <span class="value">
              {{ s.providerName }}
            </span>
          </div>
          <div class="detail-row">
            <span class="label">Duration</span>
            <span class="value">
              {{ s.durationMinutes }} minutes
            </span>
          </div>
        </div>

        <!-- AC-3: Claim button -->
        <div class="actions">
          <button mat-raised-button
                  color="primary"
                  (click)="claim()"
                  [disabled]="claiming() || expired()">
            @if (claiming()) {
              <mat-spinner diameter="20"
                           class="btn-spinner">
              </mat-spinner>
              Claiming...
            } @else {
              Claim Appointment
            }
          </button>
        </div>
      </mat-card-content>
    </mat-card>
  }
</div>
```

```scss
// slot-claim-page.component.scss

// UXR-301: Responsive layout
.claim-container {
  display: flex;
  justify-content: center;
  padding: 24px 16px;
  min-height: 60vh;
  align-items: flex-start;
}

.claim-card {
  max-width: 480px;
  width: 100%;
}

mat-card-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
  align-items: center;
  text-align: center;
  padding-top: 16px;
}

.slot-details {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 8px;

  .detail-row {
    display: flex;
    justify-content: space-between;
    padding: 8px 0;
    border-bottom: 1px solid #E0E0E0;

    .label {
      font-weight: 500;
      color: #616161;
    }
    .value {
      font-weight: 600;
    }
  }
}

.actions {
  width: 100%;
  display: flex;
  justify-content: center;
  padding-top: 8px;
}

.btn-spinner {
  display: inline-block;
  margin-right: 8px;
}

.success-icon {
  font-size: 48px;
  width: 48px;
  height: 48px;
  color: #4CAF50;
}

.expired-icon {
  font-size: 48px;
  width: 48px;
  height: 48px;
  color: #9E9E9E;
}

.loading-state {
  display: flex;
  justify-content: center;
  padding: 48px 0;
}

// UXR-301: Breakpoints
@media (max-width: 375px) {
  .claim-container {
    padding: 16px 8px;
  }
  .claim-card {
    max-width: 100%;
  }
}
```

5. **Enhance `WaitlistViewComponent`** with inline countdown:

```typescript
// Additions to waitlist-view.component.ts
// Import ClaimCountdownComponent for inline display
import { ClaimCountdownComponent } from
  './claim-countdown.component';

// In template — for entries with status 'Offered':
// Render ClaimCountdownComponent inline in the card
```

```html
<!-- Addition to waitlist-view.component.html -->
<!-- In each waitlist entry card: -->
@if (entry.status === 'Offered' && entry.expiresAtUtc) {
  <app-claim-countdown
    [expiresAtUtc]="entry.expiresAtUtc">
  </app-claim-countdown>
  <a mat-raised-button
     color="primary"
     [routerLink]="['/claim']"
     [queryParams]="{ token: entry.claimToken }">
    Claim Now
  </a>
}
```

6. **Add route**:

```typescript
// In app.routes.ts
{
  path: 'claim',
  loadComponent: () =>
    import(
      './features/waitlist/slot-claim-page.component'
    ).then(m => m.SlotClaimPageComponent)
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts                                (modify)
            └── features/
                └── waitlist/
                    ├── waitlist-view.component.ts            (modify)
                    ├── waitlist-view.component.html          (modify)
                    ├── slot-claim-page.component.ts          (new)
                    ├── slot-claim-page.component.html        (new)
                    ├── slot-claim-page.component.scss        (new)
                    ├── claim-countdown.component.ts          (new)
                    ├── claim-countdown.component.html        (new)
                    ├── claim-countdown.component.scss        (new)
                    ├── slot-claim-api.service.ts             (new)
                    └── models/
                        └── slot-claim.models.ts              (new)
```

> Placeholder: Update on execution based on US_030 task_001 and US_023 task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/waitlist/models/slot-claim.models.ts | Interfaces for claim details, urgency colors, countdown types |
| CREATE | client/src/app/features/waitlist/slot-claim-api.service.ts | HttpClient GET claim-details and POST claim |
| CREATE | client/src/app/features/waitlist/claim-countdown.component.ts | Reusable countdown with requestAnimationFrame, urgency colors |
| CREATE | client/src/app/features/waitlist/claim-countdown.component.html | Timer display with role="timer" and aria-live |
| CREATE | client/src/app/features/waitlist/claim-countdown.component.scss | Urgency color backgrounds, pulse animation, SR-only class |
| CREATE | client/src/app/features/waitlist/slot-claim-page.component.ts | Claim page resolving token, showing slot details, claim button |
| CREATE | client/src/app/features/waitlist/slot-claim-page.component.html | Template with countdown, details, success/expired states |
| CREATE | client/src/app/features/waitlist/slot-claim-page.component.scss | Responsive layout, slot detail rows, icon styles |
| MODIFY | client/src/app/features/waitlist/waitlist-view.component.ts | Import ClaimCountdownComponent for inline offered entries |
| MODIFY | client/src/app/features/waitlist/waitlist-view.component.html | Add inline countdown and Claim Now button for offered entries |
| MODIFY | client/src/app/app.routes.ts | Add /claim route for SlotClaimPageComponent |

## External References

- requestAnimationFrame: https://developer.mozilla.org/en-US/docs/Web/API/window/requestAnimationFrame
- Angular Signals: https://angular.dev/guide/signals
- ARIA Timer Role: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Roles/timer_role
- WCAG 2.1 AA Contrast: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Test claim flow:
# 1. Navigate to waitlist view
# 2. Open claim link from email: http://localhost:4200/claim?token=...
# 3. Verify countdown, urgency colors, claim button
```

## Implementation Validation Strategy

- [ ] Claim page displays slot details and countdown timer from UTC expiry (AC-2)
- [ ] Countdown shows green > 1h, amber 30m–1h, red < 30m (UXR-112, UXR-404)
- [ ] Claim button reserves slot and shows success confirmation (AC-3)
- [ ] Expired link shows 410 message with return-to-waitlist link (AC-4)
- [ ] Countdown converts UTC to browser timezone (edge case 2)
- [ ] Timer has role="timer" and aria-live announcements (UXR-203)
- [ ] Layout responsive at 375px, 768px, 1440px (UXR-301)
- [ ] Text meets WCAG AA 4.5:1 contrast ratio (UXR-201)

## Implementation Checklist

- [ ] Create TypeScript interfaces for claim details, urgency colors, countdown types
- [ ] Implement SlotClaimApiService with GET claim-details and POST claim
- [ ] Build ClaimCountdownComponent with requestAnimationFrame and urgency color transitions
- [ ] Implement SlotClaimPageComponent with token resolution, claim flow, and error states
- [ ] Add role="timer" and aria-live screen reader announcements
- [ ] Enhance WaitlistViewComponent with inline countdown for offered entries
- [ ] Add /claim route in app.routes.ts
- [ ] Verify responsiveness at mobile, tablet, and desktop breakpoints
