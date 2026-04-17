---
task_id: task_001
user_story: us_032
epic: EP-004
layer: Frontend
status: not-started
effort_hours: 5
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_032] Staff Arrival Check-In Workflow
- **Story Location**: [.propel/context/tasks/EP-004/us_032/us_032.md](.propel/context/tasks/EP-004/us_032/us_032.md)
- **Acceptance Criteria**:
  - AC-1: "Check In" button transitions appointment from "Scheduled" → "Arrived" and records timestamp.
  - AC-2: "Start Visit" button transitions "Arrived" → "In-Progress" and records visit start time.
  - AC-3: "Complete Visit" button transitions "In-Progress" → "Completed" and records visit end time.
  - AC-4: "No-Show" button transitions to "No-Show"; audit log records the acting staff member.
- **Edge Cases**:
  - Edge Case 1: State transition performed out of order → state machine rejects with descriptive error; frontend shows `app-toast[error]` with the server error message.
  - Edge Case 2: Patient not in queue → walk-in creation flow triggered (US_033); out of scope for this task.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-025-queue-dashboard.[html\|png\|jpg]` or provide external URL |
| **Screen Spec** | [figma_spec.md#SCR-025](.propel/context/docs/figma_spec.md#SCR-025) |
| **UXR Requirements** | UXR-106, UXR-111, UXR-201, UXR-202, UXR-501 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#transitions](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **PENDING**: UI-impacting task awaiting wireframe — provide file or URL before pixel-perfect validation.
>
> **Note**: us_032 references SCR-015 (Clinical Timeline). Corrected to SCR-025 (Queue Dashboard) per figma_spec.md — action buttons appear as row-level actions on the queue table defined in SCR-025.

### UXR Constraints Applied

| UXR | Constraint | Implementation |
|-----|------------|----------------|
| UXR-111 | Destructive actions MUST require confirmation dialog | "No-Show" button opens `app-confirm-dialog[variant="destructive"]` |
| UXR-501 | Buttons MUST show loading spinner and disable during network request | Each action button sets `loading = true`, disables until API response |
| UXR-106 | Queue MUST display real-time status with color-coded badges | After successful transition, badge updates in-place without reload |
| UXR-201 | Consistent interaction feedback | Toast success/error messages on every state transition |
| UXR-202 | Clear affordance for primary actions | "Check In" uses `app-button[variant="primary"]`; destructive "No-Show" uses `app-button[variant="destructive"]` |

### Screen States (SCR-025 — action buttons affect row validation state)

| State | Description |
|-------|-------------|
| Default | Row displays contextual action buttons based on current `QueueState` |
| Loading | Action button shows inline spinner; row disabled; no stale interactions |
| Error | `app-toast[error]` with server error message; row reverts to pre-action state |
| Validation | `app-toast[success]` on successful transition; badge animates to new status color |
| Confirmation | `app-confirm-dialog` opens before No-Show action is committed |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
| Reactive | RxJS | 7.x |
| HTTP Client | Angular HttpClient | 17.x |
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL | 15.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
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

Add contextual state-transition action buttons to each row of the `QueueDashboardComponent` (from us_031 task_001). The buttons rendered per row depend on the appointment's current `QueueState`: "Scheduled" rows show "Check In"; "Arrived" rows show "Start Visit"; "In-Progress" rows show "Complete Visit" and "No-Show". Clicking any button calls `PATCH /api/v1/appointments/{id}/state` and sets the button to a loading/disabled state (UXR-501). "No-Show" requires an `app-confirm-dialog` before submitting (UXR-111). On success, the row's status badge updates in-place and a success toast appears. On error (invalid transition, network failure), an error toast shows the server message and the row reverts.

---

## Dependent Tasks

- **us_031 task_001** — `QueueDashboardComponent` must exist as the host component; action buttons are added to its row template.
- **task_002** (us_032) — `IAppointmentStateMachineService` must be deployed (or mocked via Angular HTTP interceptor) before action buttons can be validated end-to-end.
- **task_003** (us_032) — `PATCH /api/v1/appointments/{id}/state` endpoint must be available.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `QueueDashboardComponent` | MODIFY | Add action buttons column to table row template |
| `CheckinActionsComponent` | CREATE | Standalone row-level actions component encapsulating button logic |
| `AppointmentStateService` | CREATE | Angular service wrapping `PATCH /api/v1/appointments/{id}/state` |
| `app-confirm-dialog` | USE | Existing design-system component — no modification |
| `app-toast` | USE | Existing design-system component — no modification |
| `app-button` | USE | Existing design-system component — no modification |

---

## Implementation Plan

1. **Create `AppointmentStateService`** in `app/features/queue/appointment-state.service.ts`: method `transitionState(appointmentId: string, action: 'check-in' | 'start-visit' | 'complete-visit' | 'no-show'): Observable<QueueEntry>` — delegates to `HttpClient.patch<QueueEntry>('/api/v1/appointments/${id}/state', { action })`.
2. **Create `CheckinActionsComponent`** as a standalone Angular component accepting `@Input() entry: QueueEntry` and emitting `@Output() stateChanged = new EventEmitter<QueueEntry>()`.
3. **Implement action button rendering logic**: Use `@switch (entry.status)` to conditionally render the correct button set:
   - `Scheduled` → `<app-button variant="primary">Check In</app-button>`
   - `Arrived` → `<app-button variant="primary">Start Visit</app-button>`
   - `InProgress` → `<app-button variant="secondary">Complete Visit</app-button>` + `<app-button variant="destructive">No-Show</app-button>`
4. **Implement UXR-501 loading state**: Track a `loading = signal(false)` per component instance; set `true` on click, `false` on response; bind `[disabled]="loading()"` and `[loading]="loading()"` to `app-button`.
5. **Implement UXR-111 confirmation for No-Show**: Before calling the service, open `app-confirm-dialog` with message "Mark [PatientName] as No-Show? This action will be recorded in the audit log." Only proceed if user confirms.
6. **Handle success**: On `next`, emit `stateChanged` with the updated entry; parent `QueueDashboardComponent` updates the entry in the queue signal; show `app-toast[success]` "Status updated to [NewStatus]".
7. **Handle errors**: On `error`, show `app-toast[error]` with `error.error.message` (server-supplied description); reset `loading` signal; do NOT update row state.
8. **Add action column to `QueueDashboardComponent`**: Include `<app-checkin-actions [entry]="row" (stateChanged)="onStateChanged($event)">` in the table row template; handle `onStateChanged` to replace the matching entry in the `queue` signal.

---

## Current Project State

```
app/
├── features/
│   └── queue/
│       ├── queue-dashboard.component.ts        ← MODIFY (add action column + onStateChanged handler)
│       ├── queue-dashboard.component.html      ← MODIFY (add <app-checkin-actions> to row template)
│       ├── checkin-actions.component.ts        ← CREATE
│       ├── checkin-actions.component.html      ← CREATE
│       ├── checkin-actions.component.scss      ← CREATE
│       └── appointment-state.service.ts        ← CREATE
└── shared/
    └── models/
        └── queue-entry.model.ts                ← MODIFY (confirm QueueEntry has status field)
```

> Placeholder: Update this tree once us_031 task_001 completes and the actual file structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/queue/checkin-actions.component.ts` | Standalone component with `@Input() entry`, `@Output() stateChanged`, loading signal, confirm dialog |
| CREATE | `app/features/queue/checkin-actions.component.html` | `@switch` button rendering; `app-confirm-dialog` for No-Show |
| CREATE | `app/features/queue/checkin-actions.component.scss` | Button row spacing using `spacing.sm` (8px) gap |
| CREATE | `app/features/queue/appointment-state.service.ts` | `transitionState()` wrapping `PATCH /api/v1/appointments/{id}/state` |
| MODIFY | `app/features/queue/queue-dashboard.component.ts` | Import `CheckinActionsComponent`; add `onStateChanged()` handler updating queue signal |
| MODIFY | `app/features/queue/queue-dashboard.component.html` | Add `actions` column with `<app-checkin-actions>` in `app-data-table` row |

---

## External References

- Angular Signals `signal` + `EventEmitter` pattern: https://angular.dev/guide/signals (Angular 17)
- Angular `@switch` control flow (Angular 17): https://angular.dev/guide/templates/control-flow#switch-blocks
- Angular `HttpClient.patch` with typed response: https://angular.io/api/common/http/HttpClient#patch (Angular 17)
- WCAG 2.2 AA: Confirm dialog keyboard trap pattern: https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- NFR-010: Audit evidence for state changes — enforced in backend (task_002); frontend surfaces audit confirmation text in No-Show dialog

---

## Build Commands

```bash
# Development server
ng serve

# Production build
ng build --configuration production

# Lint
ng lint
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass for `CheckinActionsComponent` (button visibility per QueueState, loading state, error reset)
- [ ] Unit tests pass for `AppointmentStateService` (correct PATCH URL and body per action)
- [ ] "Check In" button only visible for `Scheduled` entries; "Start Visit" only for `Arrived`; etc.
- [ ] Button disables and shows spinner during PATCH request (UXR-501)
- [ ] `app-confirm-dialog` opens for No-Show; action cancelled if user dismisses (UXR-111)
- [ ] Success toast shown with new status name; row badge updates in-place without reload
- [ ] Error toast shows server error message; row reverts to original state
- [ ] **[UI Task]** Visual comparison against SCR-025 wireframe at 375px, 768px, 1440px when wireframe available
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment once wireframe uploaded

---

## Implementation Checklist

- [ ] Create `AppointmentStateService` with `transitionState(id, action)` method returning `Observable<QueueEntry>`
- [ ] Create `CheckinActionsComponent` with `@Input() entry` and `@Output() stateChanged`
- [ ] Implement `@switch (entry.status)` button rendering (Check In / Start Visit / Complete Visit + No-Show)
- [ ] Implement UXR-501: per-component `loading = signal(false)` bound to button `[disabled]` and `[loading]`
- [ ] Implement UXR-111: `app-confirm-dialog` for No-Show with audit-context message
- [ ] Implement success handler: emit `stateChanged`, show `app-toast[success]`
- [ ] Implement error handler: show `app-toast[error]` with server message; reset loading; do not mutate row
- [ ] Wire `CheckinActionsComponent` into `QueueDashboardComponent` table row template
- [ ] **[UI Task — MANDATORY]** Reference SCR-025 wireframe from Design References table when available
- [ ] **[UI Task — MANDATORY]** Validate UI matches wireframe at 375px, 768px, 1440px before marking complete
