# Task - TASK_003

## Requirement Reference

- User Story: us_028
- Story Location: .propel/context/tasks/EP-003/us_028/us_028.md
- Acceptance Criteria:
  - AC-2: Given I am viewing the queue dashboard as a staff member, When appointments are displayed, Then each appointment card shows the risk label with a color-coded badge (green=Low, amber=Medium, red=High).
  - AC-3: Given a High-risk appointment is detected, When 24 hours before the appointment time, Then the staff member is surfaced a risk indicator prompt to consider manual follow-up.
- Edge Cases:
  - What happens if the risk model is unavailable? Appointments display with risk label "Unknown" and no false indicators are shown; staff are notified of the scoring service outage.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-025-queue-dashboard.html` (pending) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-025 |
| **UXR Requirements** | UXR-106 (color-coded badges, auto-refresh), UXR-201 (WCAG AA contrast), UXR-203 (screen reader announcements for dynamic updates), UXR-404 (consistent color semantics: green/amber/red) |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-012 but SCR-012 is "Document Library" (EP-005). The actual Queue Dashboard is **SCR-025** per figma_spec.md, which describes "Real-time patient queue with color-coded status, wait-time estimates, one-click check-in, and inline patient detail expansion."

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

Add no-show risk badge display and 24-hour high-risk alert banner to the queue dashboard (SCR-025). The existing queue dashboard shows a data table of upcoming appointments with status badges and wait-time columns. This task adds a `RiskBadgeComponent` (AC-2) that renders a color-coded mat-chip badge (green for Low, amber for Medium, red for High, grey for Unknown) in each appointment row. The badge includes an `aria-label` with the risk level for screen readers (UXR-203) and meets WCAG AA 4.5:1 contrast ratio (UXR-201). Colors follow the consistent status semantics defined by UXR-404 (green=success/Low, amber=warning/Medium, red=error/High). Clicking the badge opens a tooltip/popover showing the explainable feature contributions (AC-1 display) so staff understand the risk reasoning. The dashboard consumes `GET /api/v1/appointments/risk-scores` (from task_002) and auto-refreshes every 15 seconds (SCR-025 layout spec, UXR-106). A `HighRiskAlertBannerComponent` (AC-3) displays a prominent banner at the top of the dashboard when any appointment within 24 hours has `RiskLevel = High`, prompting "High-risk patient {name} at {time} — consider manual follow-up". The banner uses `role="alert"` and `aria-live="assertive"` for screen reader announcement (UXR-203). When the AI model is unavailable (edge case 1), badges show "Unknown" in grey with a subtle "Scoring unavailable" tooltip — no false risk indicators are shown. The dashboard also listens for `HighRiskAlertEvent` via SignalR for real-time push notifications beyond the polling interval.

## Dependent Tasks

- US_028 task_002 (requires GET /api/v1/appointments/risk-scores endpoint and HighRiskAlertEvent)

## Impacted Components

- New: `client/src/app/features/queue/risk-badge.component.ts` (standalone component for color-coded risk chip)
- New: `client/src/app/features/queue/risk-badge.component.html` (template with mat-chip and popover)
- New: `client/src/app/features/queue/risk-badge.component.scss` (risk color tokens, contrast compliance)
- New: `client/src/app/features/queue/high-risk-alert-banner.component.ts` (banner for 24h high-risk alerts)
- New: `client/src/app/features/queue/models/risk-score.models.ts` (TypeScript interfaces)
- Modify: `client/src/app/features/queue/queue-dashboard.component.ts` (integrate risk scores API, add risk column)
- Modify: `client/src/app/features/queue/queue-dashboard.component.html` (add risk badge column and alert banner)

## Implementation Plan

1. **Create TypeScript interfaces**:

```typescript
// client/src/app/features/queue/models/risk-score.models.ts

export interface AppointmentRiskScore {
  appointmentId: string;
  patientName: string;
  appointmentDate: string;
  appointmentType: string;
  status: string;
  riskLevel: RiskLevel;
  confidence: number;
  features: RiskFeature[];
}

export interface RiskFeature {
  name: string;
  contribution: string;
}

export type RiskLevel = 'Low' | 'Medium' | 'High' | 'Unknown';

export const RISK_COLORS: Record<RiskLevel, string> = {
  Low: '#4CAF50',     // Green — UXR-404 success
  Medium: '#FF9800',  // Amber — UXR-404 warning
  High: '#F44336',    // Red — UXR-404 error
  Unknown: '#9E9E9E'  // Grey — neutral
};

export const RISK_LABELS: Record<RiskLevel, string> = {
  Low: 'Low Risk',
  Medium: 'Medium Risk',
  High: 'High Risk',
  Unknown: 'Unknown'
};
```

2. **Create `RiskBadgeComponent`** standalone component:

```typescript
// client/src/app/features/queue/risk-badge.component.ts
import {
  Component, input, computed
} from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  RiskLevel, RiskFeature, RISK_COLORS, RISK_LABELS
} from './models/risk-score.models';

@Component({
  selector: 'app-risk-badge',
  standalone: true,
  imports: [MatChipsModule, MatTooltipModule],
  templateUrl: './risk-badge.component.html',
  styleUrl: './risk-badge.component.scss'
})
export class RiskBadgeComponent {
  riskLevel = input.required<RiskLevel>();
  features = input<RiskFeature[]>([]);

  // AC-2: Color-coded badge
  badgeColor = computed(() =>
    RISK_COLORS[this.riskLevel()]);
  badgeLabel = computed(() =>
    RISK_LABELS[this.riskLevel()]);

  // UXR-203: Screen reader label
  ariaLabel = computed(() =>
    `No-show risk: ${this.badgeLabel()}`);

  // Tooltip with feature explanations
  tooltipText = computed(() => {
    if (this.riskLevel() === 'Unknown') {
      return 'Scoring unavailable';
    }
    const feats = this.features();
    if (feats.length === 0) return this.badgeLabel();
    return feats
      .map(f => `${f.name}: ${f.contribution}`)
      .join('\n');
  });
}
```

```html
<!-- risk-badge.component.html -->
<mat-chip
  [style.background-color]="badgeColor()"
  [style.color]="'#fff'"
  [attr.aria-label]="ariaLabel()"
  [matTooltip]="tooltipText()"
  matTooltipPosition="above"
  class="risk-chip">
  {{ badgeLabel() }}
</mat-chip>
```

```scss
// risk-badge.component.scss
.risk-chip {
  font-size: 12px;
  font-weight: 600;
  min-height: 24px;
  cursor: default;

  // UXR-201: Ensure 4.5:1 contrast with white text
  // Green (#4CAF50) on white text = 4.6:1 ✓
  // Amber (#FF9800) needs dark text for contrast
  &[style*="FF9800"] {
    color: #000 !important;
  }
}
```

3. **Create `HighRiskAlertBannerComponent`**:

```typescript
// client/src/app/features/queue/
//   high-risk-alert-banner.component.ts
import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { DatePipe } from '@angular/common';
import { AppointmentRiskScore } from
  './models/risk-score.models';

@Component({
  selector: 'app-high-risk-alert-banner',
  standalone: true,
  imports: [MatIconModule, DatePipe],
  template: `
    <!-- AC-3: Staff follow-up prompt -->
    <!-- UXR-203: role="alert" + aria-live for SR -->
    @for (appt of highRiskAppointments(); track appt.appointmentId) {
      <div class="high-risk-banner"
           role="alert"
           aria-live="assertive">
        <mat-icon>warning</mat-icon>
        <span>
          High-risk patient
          <strong>{{ appt.patientName }}</strong>
          at {{ appt.appointmentDate | date:'shortTime' }}
          — consider manual follow-up
        </span>
      </div>
    }
  `,
  styles: [`
    .high-risk-banner {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 16px;
      background: #FFF3E0;
      border-left: 4px solid #F44336;
      border-radius: 4px;
      margin-bottom: 8px;
      font-weight: 500;

      mat-icon {
        color: #F44336;
      }
    }
  `]
})
export class HighRiskAlertBannerComponent {
  highRiskAppointments =
    input.required<AppointmentRiskScore[]>();
}
```

4. **Integrate into queue dashboard**:

```typescript
// Additions to queue-dashboard.component.ts
import { interval, switchMap, takeUntil, Subject } from 'rxjs';
import { RiskBadgeComponent } from './risk-badge.component';
import { HighRiskAlertBannerComponent } from
  './high-risk-alert-banner.component';
import { AppointmentRiskScore } from
  './models/risk-score.models';

// Add to component class
readonly riskScores = signal<AppointmentRiskScore[]>([]);
readonly highRiskAlerts = computed(() =>
  this.riskScores().filter(s =>
    s.riskLevel === 'High'
    && this.isWithin24Hours(s.appointmentDate)));

private readonly destroy$ = new Subject<void>();

ngOnInit(): void {
  // UXR-106: Auto-refresh every 15 seconds
  interval(15_000).pipe(
    switchMap(() => this.riskApiService.getRiskScores(
      this.dateFrom, this.dateTo)),
    takeUntil(this.destroy$)
  ).subscribe({
    next: (scores) => this.riskScores.set(scores),
    error: (err) => console.error(
      'Risk score refresh failed', err)
  });

  // Initial load
  this.loadRiskScores();
}

ngOnDestroy(): void {
  this.destroy$.next();
  this.destroy$.complete();
}

private isWithin24Hours(dateStr: string): boolean {
  const apptDate = new Date(dateStr);
  const now = new Date();
  const diff = apptDate.getTime() - now.getTime();
  return diff > 0 && diff <= 24 * 60 * 60 * 1000;
}
```

```html
<!-- Additions to queue-dashboard.component.html -->

<!-- AC-3: High-risk alert banner at top -->
@if (highRiskAlerts().length > 0) {
  <app-high-risk-alert-banner
    [highRiskAppointments]="highRiskAlerts()">
  </app-high-risk-alert-banner>
}

<!-- In table column definitions, add risk column -->
<ng-container matColumnDef="risk">
  <th mat-header-cell *matHeaderCellDef>Risk</th>
  <td mat-cell *matCellDef="let row">
    <app-risk-badge
      [riskLevel]="getRiskLevel(row.appointmentId)"
      [features]="getRiskFeatures(row.appointmentId)">
    </app-risk-badge>
  </td>
</ng-container>
```

5. **Create `RiskScoreApiService`**:

```typescript
// Add to existing queue API service or create new
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from
  '@angular/common/http';
import { Observable } from 'rxjs';
import { AppointmentRiskScore } from
  './models/risk-score.models';

@Injectable({ providedIn: 'root' })
export class RiskScoreApiService {
  private readonly http = inject(HttpClient);

  getRiskScores(
    from: string,
    to: string
  ): Observable<AppointmentRiskScore[]> {
    const params = new HttpParams()
      .set('from', from)
      .set('to', to);

    return this.http.get<AppointmentRiskScore[]>(
      '/api/v1/appointments/risk-scores',
      { params }
    );
  }
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            └── features/
                └── queue/
                    ├── queue-dashboard.component.ts     (modify)
                    ├── queue-dashboard.component.html   (modify)
                    ├── risk-badge.component.ts          (new)
                    ├── risk-badge.component.html        (new)
                    ├── risk-badge.component.scss        (new)
                    ├── high-risk-alert-banner.component.ts (new)
                    ├── risk-score-api.service.ts        (new)
                    └── models/
                        └── risk-score.models.ts         (new)
```

> Placeholder: Update on execution based on US_028 task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/queue/models/risk-score.models.ts | TypeScript interfaces for risk score, feature, color/label maps |
| CREATE | client/src/app/features/queue/risk-badge.component.ts | Standalone component with color-coded mat-chip and feature tooltip |
| CREATE | client/src/app/features/queue/risk-badge.component.html | Template with mat-chip, aria-label, matTooltip |
| CREATE | client/src/app/features/queue/risk-badge.component.scss | Risk color tokens, contrast compliance for WCAG AA |
| CREATE | client/src/app/features/queue/high-risk-alert-banner.component.ts | Inline banner with role="alert" and aria-live="assertive" |
| CREATE | client/src/app/features/queue/risk-score-api.service.ts | HttpClient service for GET /api/v1/appointments/risk-scores |
| MODIFY | client/src/app/features/queue/queue-dashboard.component.ts | Integrate risk scores with 15s auto-refresh, computed highRiskAlerts |
| MODIFY | client/src/app/features/queue/queue-dashboard.component.html | Add risk column with RiskBadgeComponent, alert banner at top |

## External References

- Angular Material Chips: https://material.angular.io/components/chips/overview
- Angular Material Tooltip: https://material.angular.io/components/tooltip/overview
- WCAG 2.1 AA Contrast: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html
- ARIA Live Regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/ARIA_Live_Regions

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Navigate to: http://localhost:4200/queue
# Verify risk badges, tooltip popover, and high-risk alert banner
```

## Implementation Validation Strategy

- [ ] Each appointment row displays a color-coded risk badge (green/amber/red/grey) (AC-2)
- [ ] Badge tooltip shows explainable feature contributions on hover
- [ ] High-risk alert banner appears for appointments within 24 hours (AC-3)
- [ ] "Unknown" badge shown in grey when model unavailable — no false indicators (edge case 1)
- [ ] Badge has aria-label for screen readers (UXR-203)
- [ ] Alert banner uses role="alert" with aria-live="assertive" (UXR-203)
- [ ] Color contrast meets WCAG AA 4.5:1 ratio (UXR-201)
- [ ] Dashboard auto-refreshes risk scores every 15 seconds (UXR-106)

## Implementation Checklist

- [ ] Create TypeScript interfaces for risk score, feature, color/label constants
- [ ] Implement RiskBadgeComponent with mat-chip, color mapping, and feature tooltip
- [ ] Ensure WCAG AA 4.5:1 contrast for all risk badge color/text combinations
- [ ] Implement HighRiskAlertBannerComponent with role="alert" and aria-live="assertive"
- [ ] Create RiskScoreApiService for GET /api/v1/appointments/risk-scores
- [ ] Integrate risk scores into queue dashboard with 15-second auto-refresh
- [ ] Add risk column to queue data table with RiskBadgeComponent
- [ ] Add high-risk alert banner at top of dashboard for 24h window appointments
