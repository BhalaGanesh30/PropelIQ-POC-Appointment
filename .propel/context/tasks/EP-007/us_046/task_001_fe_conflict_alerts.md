---
task_id: task_001
user_story: us_046
epic: EP-007
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_046] Drug-Drug and Drug-Allergy Conflict Detection
- **Story Location**: [.propel/context/tasks/EP-007/us_046/us_046.md](.propel/context/tasks/EP-007/us_046/us_046.md)
- **Acceptance Criteria**:
  - AC-1: Given a patient profile contains medications and allergies, When the conflict detection engine evaluates clinical facts, Then drug-drug and drug-allergy conflicts are identified and classified by severity (Low, Moderate, High, Critical).
  - AC-2: Given a conflict is detected, When the patient profile is displayed, Then conflict alert banners appear above the medications section with the severity label and a description of the conflict.
  - AC-3: Given a Critical severity conflict is identified, When the clinician views the profile, Then a mandatory acknowledgment dialog is shown and the clinician cannot dismiss the profile without explicitly acknowledging the critical alert.
  - AC-4: Given the clinician acknowledges a critical conflict, When the acknowledgment is recorded, Then it is logged in the audit trail with the clinician's identity and timestamp.
- **Edge Cases**:
  - Edge Case 1: Conflict detection rules database is outdated — API returns `rulesStale: true`; a staleness warning banner is shown in the UI; no blocking of alert display.
  - Edge Case 2: 20+ medications producing many conflict pairs — conflicts are deduplicated to unique pairs; only the highest severity per drug pair is displayed to avoid alert fatigue.

---

## Design References (UI Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | `.propel/context/docs/figma_spec.md#SCR-016` |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe — upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-016-conflict-alerts.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | SCR-016 (Conflict Alerts) |
| **UXR Requirements** | UXR-111, UXR-201, UXR-203, UXR-206, UXR-404 |
| **Design Tokens** | Refer to global design tokens (severity colors: red/critical, orange/high, yellow/moderate, blue/low) |

> **Note — Screen ID Correction**: US_046 references SCR-025 (Queue Dashboard) in the story file. The correct screen for conflict alerts is **SCR-016**. SCR-025 belongs to the Staff Queue Dashboard (EP-004). All implementation targets SCR-016.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Component Library | Angular Material + CDK | 17.x |
| State/Reactivity | Angular Signals + RxJS | 7.x |
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL | 15.x |
| AI/ML | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement the `ConflictAlertsComponent` (SCR-016) embedded within the Conflicts tab of the 360° Patient Profile (SCR-014). The component calls `GET /api/v1/patients/{id}/conflicts` on tab activation and renders alert cards sorted by severity — Critical first (AC-1). Each card shows a severity-colored left border (red: critical, orange: high, yellow: moderate, blue: low per UXR-404), a severity badge, a conflict description, and the conflicting drug pair or drug-allergy pair (AC-2). Acknowledged alerts move to a collapsible "Resolved" section below active alerts. For Critical severity alerts, a mandatory `app-confirm-dialog[variant="Destructive"]` is shown immediately on profile view — the clinician must explicitly acknowledge before interacting with the rest of the profile (AC-3, UXR-111). The confirmation dialog uses typed confirmation text and focus trapping per UXR-206. Screen reader announcements are emitted for new alert banners and acknowledgment outcomes using `aria-live` regions (UXR-203). If `rulesStale: true` is returned in the API response, a non-blocking amber warning banner is displayed (Edge Case 1). Loading state shows skeleton alert cards; empty state shows "No conflicts detected" success message; error state shows a retry banner (SCR-016 states). WCAG 2.1 AA contrast on all severity badges and borders (UXR-201). Focus returned to the trigger button after dialog close (UXR-206).

---

## Dependent Tasks

- **us_046/task_002** — `GET /api/v1/patients/{id}/conflicts` and `POST /api/v1/conflicts/{id}/acknowledge` APIs must be implemented.
- **us_045/task_001** — Patient profile tab shell (SCR-014) must exist to host the Conflicts tab.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ConflictAlertsComponent` | CREATE | Conflicts tab content: loads on tab activation, renders alert card list |
| `ConflictAlertCardComponent` | CREATE | Single alert card: severity badge, colored left border, description, conflicting items, acknowledge button |
| `ConflictAcknowledgeDialogComponent` | CREATE | `app-confirm-dialog[variant="Destructive"]` wrapper: typed confirmation, `MatDialog`, focus trap (UXR-111, UXR-206) |
| `RulesStaleWarningComponent` | CREATE | Non-blocking amber banner shown when `rulesStale: true` (Edge Case 1) |
| `ConflictAlertsSkeletonComponent` | CREATE | Skeleton loader matching alert card layout (SCR-016 Loading state) |
| `ConflictEmptyStateComponent` | CREATE | "No conflicts detected" success message (SCR-016 Empty state) |
| `ConflictAlertsService` | CREATE | Angular service: `getConflicts(patientId)`, `acknowledgeConflict(conflictId)` via HttpClient |
| `ConflictAlertsFacade` | CREATE | Signal-based state: alerts, loading, error, rulesStale, pendingCritical |
| `ProfileConflictsTabComponent` | CREATE | Tab wrapper hosting `ConflictAlertsComponent`; triggers data load on tab activation |
| `PatientProfileComponent` | MODIFY | Add Conflicts tab to `mat-tab-group`; block tab-switch when unacknowledged Critical alert pending (AC-3) |

---

## Implementation Plan

1. **Create `ConflictAlertsService`**: `getConflicts(patientId: string): Observable<ConflictAlertsResponseDto>` — `HttpClient.get('/api/v1/patients/${patientId}/conflicts')`. `acknowledgeConflict(conflictId: string): Observable<void>` — `HttpClient.post('/api/v1/conflicts/${conflictId}/acknowledge', {})`. Both use typed DTOs: `ConflictAlertDto` (conflictId, conflictType, severity, description, drugA, drugB, acknowledged), `ConflictAlertsResponseDto` (alerts: ConflictAlertDto[], rulesStale: boolean).
2. **Create `ConflictAlertsFacade`**: Signal state: `alerts = signal<ConflictAlertDto[]>([])`, `loading = signal(false)`, `error = signal<string | null>(null)`, `rulesStale = signal(false)`, `pendingCritical = computed(() => alerts().filter(a => a.severity === 'critical' && !a.acknowledged))`. `loadConflicts(patientId)` method: set loading, call service, update all signals. `acknowledge(conflictId)` method: call service, mark alert acknowledged in local signal, remove from `pendingCritical`.
3. **Create `ConflictAlertCardComponent`**: Input: `[alert]: ConflictAlertDto`. Display: severity badge (`[attr.data-severity]="alert.severity"` for CSS), colored left border via CSS custom properties mapped from severity, conflict description, drug pair labels. Output: `(acknowledge): EventEmitter<string>`. Acknowledge button triggers parent acknowledge flow. If `alert.acknowledged`, show "Acknowledged" chip instead.
4. **Create `ConflictAcknowledgeDialogComponent`**: Wraps `app-confirm-dialog[variant="Destructive"]`. Accepts `data: { conflictId, severity, description }` via `MAT_DIALOG_DATA`. For Critical: requires the clinician to type "ACKNOWLEDGE" in a text input before enabling the confirm button (typed confirmation per SCR-016 Validation state). On confirm: emits `true`; on cancel: emits `false`. Uses `cdkTrapFocus` directive on the dialog container (UXR-206). Emits focus back to trigger element on close.
5. **Create `ConflictAlertsComponent`**: On init, call `ConflictAlertsFacade.loadConflicts(patientId)`. Show `ConflictAlertsSkeletonComponent` while loading. Show `ConflictEmptyStateComponent` when `alerts().length === 0` and not loading. Show `RulesStaleWarningComponent` when `rulesStale()` is true. Render active alerts using `@for (alert of activeAlerts(); track alert.conflictId)` sorted Critical → High → Moderate → Low. Render acknowledged alerts in a collapsible `<mat-expansion-panel>` labeled "Resolved (n)". Add `aria-live="polite"` region wrapping the alerts list for screen reader announcements (UXR-203).
6. **Handle mandatory Critical acknowledgment (AC-3)**: In `ConflictAlertsComponent.ngOnInit()`, check `pendingCritical()` computed signal. If non-empty, open `ConflictAcknowledgeDialogComponent` via `MatDialog.open()` with `disableClose: true`. On dialog confirm: call `ConflictAlertsFacade.acknowledge(conflictId)`. Chain through all pending critical alerts in sequence. `PatientProfileComponent` checks for pending critical alerts on tab-switch and blocks navigation using `(selectedTabChange)` guard until all are acknowledged (AC-3).
7. **Create `RulesStaleWarningComponent`**: Amber `<mat-card>` with warning icon, text "Conflict detection rules may be outdated. Results could be incomplete.", and a dismiss button. Use `UXR-404` amber color token.
8. **Accessibility**: Wrap alerts list in `<div role="status" aria-live="polite" aria-atomic="false">` so each new alert card is announced (UXR-203). Add `aria-label` to acknowledge button: `"Acknowledge {{ alert.severity }} conflict: {{ alert.description }}"` (UXR-201). Apply `cdkTrapFocus` in dialog (UXR-206). Verify severity badge colors pass 4.5:1 contrast on both light and dark backgrounds (UXR-201). Apply `@focus-visible` CSS on all interactive elements (UXR-201).

---

## Current Project State

```
src/
├── app/
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   ├── document-upload/              ← EXISTS (US_040)
│   │   │   │   ├── ocr-status/                   ← EXISTS (US_041)
│   │   │   │   ├── document-viewer/              ← EXISTS (US_042)
│   │   │   │   ├── document-library/             ← EXISTS (US_043)
│   │   │   │   ├── patient-profile/              ← EXISTS (US_045)
│   │   │   │   │   ├── patient-profile.component.ts           ← MODIFY (add Conflicts tab; block tab-switch for pending Critical)
│   │   │   │   │   └── tabs/
│   │   │   │   │       └── profile-conflicts-tab.component.ts ← CREATE
│   │   │   │   └── conflict-alerts/              ← CREATE
│   │   │   │       ├── conflict-alerts.component.ts           ← CREATE
│   │   │   │       ├── conflict-alerts.component.html         ← CREATE
│   │   │   │       ├── conflict-alerts.component.scss         ← CREATE
│   │   │   │       ├── conflict-alert-card.component.ts       ← CREATE
│   │   │   │       ├── conflict-acknowledge-dialog.component.ts  ← CREATE
│   │   │   │       ├── rules-stale-warning.component.ts       ← CREATE
│   │   │   │       ├── conflict-alerts-skeleton.component.ts  ← CREATE
│   │   │   │       └── conflict-empty-state.component.ts      ← CREATE
│   │   │   ├── services/
│   │   │   │   ├── patient-profile.service.ts    ← EXISTS (US_045)
│   │   │   │   └── conflict-alerts.service.ts    ← CREATE
│   │   │   └── facades/
│   │   │       ├── patient-profile.facade.ts     ← EXISTS (US_045)
│   │   │       └── conflict-alerts.facade.ts     ← CREATE
│   │   └── [existing modules...]
│   └── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/modules/clinical-intelligence/components/conflict-alerts/conflict-alerts.component.ts` | Conflicts list: load on activate, sort by severity, live region, empty/loading/error/stale states |
| CREATE | `app/modules/clinical-intelligence/components/conflict-alerts/conflict-alerts.component.html` | Template: skeleton, stale warning, active alerts, resolved panel, live region |
| CREATE | `app/modules/clinical-intelligence/components/conflict-alerts/conflict-alerts.component.scss` | Severity left borders (CSS custom props), badge colors, resolved section styles |
| CREATE | `app/modules/clinical-intelligence/components/conflict-alerts/conflict-alert-card.component.ts` | Alert card: severity badge, description, drug pair, acknowledge button |
| CREATE | `app/modules/clinical-intelligence/components/conflict-alerts/conflict-acknowledge-dialog.component.ts` | MatDialog wrapper: typed "ACKNOWLEDGE" confirmation for Critical, cdkTrapFocus |
| CREATE | `app/modules/clinical-intelligence/components/conflict-alerts/rules-stale-warning.component.ts` | Amber staleness warning banner (Edge Case 1) |
| CREATE | `app/modules/clinical-intelligence/components/conflict-alerts/conflict-alerts-skeleton.component.ts` | Skeleton loader matching alert card layout |
| CREATE | `app/modules/clinical-intelligence/components/conflict-alerts/conflict-empty-state.component.ts` | "No conflicts detected" success state |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/tabs/profile-conflicts-tab.component.ts` | Tab wrapper: lazy-loads ConflictAlertsComponent on tab activation |
| CREATE | `app/modules/clinical-intelligence/services/conflict-alerts.service.ts` | HttpClient service: getConflicts, acknowledgeConflict |
| CREATE | `app/modules/clinical-intelligence/facades/conflict-alerts.facade.ts` | Signal state: alerts, loading, error, rulesStale, pendingCritical computed |
| MODIFY | `app/modules/clinical-intelligence/components/patient-profile/patient-profile.component.ts` | Add Conflicts tab; block tab-switch on unacknowledged Critical alerts (AC-3) |

---

## External References

- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- Angular CDK Focus Trap: https://material.angular.io/cdk/a11y/overview#focustrap
- Angular Signals computed: https://angular.dev/guide/signals#computed-signals
- aria-live regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Attributes/aria-live
- WCAG 2.1 AA contrast: https://www.w3.org/TR/WCAG21/#contrast-minimum
- FR-CA-003: Detect drug-drug and drug-allergy conflicts, classify severity, require acknowledgment of critical alerts
- NFR-010: Immutable audit evidence for coding decisions and overrides
- UXR-111: Confirmation dialog required for destructive/critical actions
- UXR-201: WCAG 2.1 AA color contrast
- UXR-203: Screen reader announcements for dynamic content updates
- UXR-206: Focus trapped in modal dialogs; returned to trigger on close
- UXR-404: Consistent color semantics (red=critical, orange=high, yellow=moderate, blue=low)
- SCR-016 spec: `.propel/context/docs/figma_spec.md#SCR-016`

---

## Build Commands

```bash
# Install dependencies
npm install

# Serve app locally
ng serve

# Run unit tests
ng test --include="**/conflict-alerts/**"

# Build production
ng build --configuration=production
```

---

## Implementation Validation Strategy

- [ ] Conflicts tab loads alert cards sorted Critical → High → Moderate → Low (AC-1)
- [ ] Each card shows severity badge, colored left border (red/orange/yellow/blue), description, drug pair (AC-2, UXR-404)
- [ ] Critical conflict: acknowledgment dialog opens automatically on profile view with `disableClose: true` (AC-3)
- [ ] Dialog requires typing "ACKNOWLEDGE" before the confirm button is enabled (SCR-016 Validation state)
- [ ] Acknowledged alerts move to collapsible "Resolved (n)" section
- [ ] `rulesStale: true` response triggers amber warning banner without blocking display (Edge Case 1)
- [ ] Deduplicated conflicts — same drug pair shows only highest severity (Edge Case 2, driven by BE)
- [ ] Loading state: skeleton alert cards visible during fetch
- [ ] Empty state: "No conflicts detected" shown when alerts array is empty
- [ ] Error state: retry banner shown on API failure
- [ ] `aria-live="polite"` region announced by screen readers when new alerts appear (UXR-203)
- [ ] Focus trapped in dialog; returned to acknowledge button after close (UXR-206)
- [ ] Severity badge colors pass 4.5:1 contrast ratio (UXR-201)
- [ ] All interactive elements keyboard-navigable with visible focus rings (UXR-201)

---

## Implementation Checklist

- [ ] Create `ConflictAlertsService` with `getConflicts()` and `acknowledgeConflict()` typed HttpClient methods
- [ ] Create `ConflictAlertsFacade` with Signal state: `alerts`, `loading`, `error`, `rulesStale`, `pendingCritical` computed
- [ ] Create `ConflictAlertCardComponent` with severity badge, color-coded left border, description, drug pair, acknowledge button
- [ ] Create `ConflictAcknowledgeDialogComponent` with typed "ACKNOWLEDGE" confirmation, `cdkTrapFocus`, `disableClose` for Critical (AC-3, UXR-111, UXR-206)
- [ ] Create `ConflictAlertsComponent` with sorted list, acknowledged section, live region, stale warning, loading/empty/error states (AC-2, UXR-203, UXR-404)
- [ ] Create `RulesStaleWarningComponent`, `ConflictAlertsSkeletonComponent`, `ConflictEmptyStateComponent`
- [ ] Modify `PatientProfileComponent` to add Conflicts tab and block tab-switch while pending Critical alerts remain unacknowledged (AC-3)
- [ ] Apply WCAG 2.1 AA: aria-labels, 4.5:1 contrast on severity colors, keyboard navigation, focus-visible rings (UXR-201/203/206)
