---
task_id: task_001
user_story: us_034
epic: EP-004
layer: Frontend
status: not-started
effort_hours: 6
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_034] Scheduling Override with Mandatory Audit
- **Story Location**: [.propel/context/tasks/EP-004/us_034/us_034.md](.propel/context/tasks/EP-004/us_034/us_034.md)
- **Acceptance Criteria**:
  - AC-1: When a scheduling constraint blocks an action, a mandatory reason dialog is shown before the action can proceed.
  - AC-2: When the staff member provides a reason and confirms, the scheduling action is completed and an audit record is created.
  - AC-3: When the staff member submits without a reason, validation displays "Override reason is required" and does not proceed.
  - AC-4: Override events are listed in the audit log with full reason and actor details when filtered by action type "Override."
- **Edge Cases**:
  - Edge Case 1: Override reason exceeds 500 characters; validation error shown with a character counter.
  - Edge Case 2: Patient role does not have override privileges; the override option is not rendered.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-027-override-dialog.[html\|png\|jpg]` or provide external URL |
| **Screen Spec** | [figma_spec.md#SCR-027](.propel/context/docs/figma_spec.md#SCR-027) |
| **UXR Requirements** | UXR-111, UXR-201, UXR-202, UXR-205, UXR-501 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **PENDING**: UI-impacting task awaiting wireframe — provide file or URL before pixel-perfect validation.
>
> **Note**: US_034 references SCR-017 (Coding Suggestion Review). The override dialog is functionally part of SCR-027 (Staff-Assisted Booking) which has the override reason field per figma_spec.md. The dialog itself is a reusable `app-confirm-dialog[variant="typed"]` component from the design system.

### Screen States (SCR-027 — Override Dialog Context)

| State | Description |
|-------|-------------|
| Default | Override option hidden unless a scheduling constraint is violated. When triggered, modal dialog with reason textarea and Confirm/Cancel buttons. |
| Loading | Spinner on Confirm button during API call (UXR-501); button disabled to prevent double submission. |
| Empty | Reason textarea empty; Confirm button disabled until reason is provided. |
| Error | Inline validation "Override reason is required" via `aria-describedby` (UXR-205); API failure toast with retry. |
| Validation | Character counter showing current/max (500). On success, toast "Override applied" and scheduling action proceeds. |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
| Reactive | RxJS | 7.x |
| Forms | Angular Reactive Forms | 17.x |
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

Implement the `OverrideReasonDialogComponent` as a reusable standalone Angular 17 component that wraps `app-confirm-dialog[variant="typed"]` from the design system. When a staff member attempts a scheduling action that violates a constraint (e.g., cancellation within 24 hours), the calling component opens this dialog. The dialog contains a mandatory reason textarea with a 500-character limit and live character counter, inline validation per UXR-205, and Confirm/Cancel action buttons. On confirmation, the dialog emits the reason string back to the caller, which then sends the override request to `POST /api/v1/scheduling/override`. The dialog is role-gated: the override trigger button is only rendered for users with the `Staff` or `Admin` role (Patient role never sees it). Additionally, implement an `OverrideAuditLogComponent` that displays override events in a filterable table within the admin audit log screen, calling `GET /api/v1/audit?actionType=Override`.

---

## Dependent Tasks

- **us_034/task_002** — `POST /api/v1/scheduling/override` and `GET /api/v1/audit?actionType=Override` endpoints must be deployed (or mocked via Angular HTTP interceptor) for end-to-end validation.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `OverrideReasonDialogComponent` | CREATE | Standalone Angular component — modal dialog with reason textarea, character counter, validation |
| `OverrideReasonDialogComponent` (template) | CREATE | Uses `app-confirm-dialog`, `app-textarea`, `app-button` from design system |
| `OverrideReasonDialogComponent` (styles) | CREATE | Dialog layout, character counter alignment, validation error styling |
| `OverrideService` | CREATE | Angular service wrapping `POST /api/v1/scheduling/override` |
| `OverrideRequest` model | CREATE | TypeScript interface: `appointmentId`, `constraintType`, `reason`, `action` |
| `OverrideResponse` model | CREATE | TypeScript interface: `overrideId`, `auditRecordId`, `status` |
| `OverrideAuditEntry` model | CREATE | TypeScript interface: `auditId`, `actorName`, `actorRole`, `constraint`, `reason`, `timestamp` |
| Staff routing / appointment detail | MODIFY | Conditionally render "Override" button when scheduling constraint is violated and user role is Staff or Admin |

---

## Implementation Plan

1. **Create `OverrideService`** in `app/features/scheduling/override.service.ts`: method `submitOverride(payload: OverrideRequest): Observable<OverrideResponse>` wrapping `HttpClient.post('/api/v1/scheduling/override', payload)`. Method `getOverrideAuditLog(params): Observable<OverrideAuditEntry[]>` wrapping `HttpClient.get('/api/v1/audit', { params: { actionType: 'Override' } })`.
2. **Create `OverrideReasonDialogComponent`** as a standalone component using Angular CDK Dialog. The component receives input data via `MAT_DIALOG_DATA` containing `constraintType` (string describing the violated constraint) and `appointmentId` (UUID). Create a reactive form with a single `reason` control: `[Validators.required, Validators.maxLength(500)]`. Display the constraint description as read-only context text above the textarea.
3. **Implement character counter**: Bind `reason.value.length` to a `<span>` below the textarea showing `{current}/500`. Apply `color: error` when count exceeds 480 (warning zone).
4. **Implement validation**: On form submit, if `reason` is empty or whitespace-only, display "Override reason is required" inline below the textarea using `aria-describedby` (UXR-205). Confirm button is disabled when form is invalid.
5. **Implement submit flow**: On Confirm click, set button to loading state (UXR-501), call `overrideService.submitOverride(payload)`. On success, close the dialog and return `{ confirmed: true, reason, overrideId }` to the caller. On API error, show `app-toast[error]` with server message and keep dialog open for retry.
6. **Implement role-based rendering**: In the appointment detail / scheduling constraint violation context, use `@if (userRole === 'Staff' || userRole === 'Admin')` to conditionally render the "Override" button. Patient role never sees the override option.
7. **Integrate with scheduling flow**: In the appointment cancel/reschedule component, when the API returns a `409 Conflict` with a constraint violation body, open the `OverrideReasonDialogComponent`. On dialog confirmation, resubmit the original action with the override payload.
8. **Create `OverrideAuditLogComponent`** (read-only table): Standalone component for the admin audit screen. Calls `overrideService.getOverrideAuditLog()` and renders a `app-data-table` with columns: Date/Time, Staff Name, Role, Overridden Constraint, Reason. Supports filtering by date range.

---

## Current Project State

```
app/
├── features/
│   ├── scheduling/
│   │   ├── override-reason-dialog.component.ts      ← CREATE
│   │   ├── override-reason-dialog.component.html     ← CREATE
│   │   ├── override-reason-dialog.component.scss     ← CREATE
│   │   ├── override-audit-log.component.ts           ← CREATE
│   │   ├── override-audit-log.component.html          ← CREATE
│   │   ├── override-audit-log.component.scss          ← CREATE
│   │   └── override.service.ts                        ← CREATE
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── override-request.model.ts                  ← CREATE
│       ├── override-response.model.ts                 ← CREATE
│       └── override-audit-entry.model.ts              ← CREATE
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual scheduling feature folder is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/scheduling/override-reason-dialog.component.ts` | Standalone dialog component with reactive form, validation, character counter |
| CREATE | `app/features/scheduling/override-reason-dialog.component.html` | Template: constraint context, reason textarea, character counter, Confirm/Cancel buttons |
| CREATE | `app/features/scheduling/override-reason-dialog.component.scss` | Dialog layout, validation error styling, character counter styles |
| CREATE | `app/features/scheduling/override-audit-log.component.ts` | Standalone table component for override audit entries |
| CREATE | `app/features/scheduling/override-audit-log.component.html` | Template: `app-data-table` with override audit columns |
| CREATE | `app/features/scheduling/override-audit-log.component.scss` | Table layout styles |
| CREATE | `app/features/scheduling/override.service.ts` | Service wrapping override submit and audit log endpoints |
| CREATE | `app/shared/models/override-request.model.ts` | `OverrideRequest` interface |
| CREATE | `app/shared/models/override-response.model.ts` | `OverrideResponse` interface |
| CREATE | `app/shared/models/override-audit-entry.model.ts` | `OverrideAuditEntry` interface |
| MODIFY | `app/features/appointments/appointment-detail.component.html` | Conditionally render "Override" button for Staff/Admin when constraint violated |

---

## External References

- Angular 17 CDK Dialog: https://material.angular.io/cdk/dialog/overview
- Angular 17 Reactive Forms validation: https://angular.dev/guide/forms/reactive-forms
- Angular `@if` control flow (Angular 17): https://angular.dev/guide/templates/control-flow
- `aria-describedby` for inline validation (UXR-205): https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA1
- UXR-111: Destructive/override actions require confirmation dialog
- UXR-501: Form buttons show loading spinner and disable during network requests
- FR-SO-004: Staff override of scheduling constraints with mandatory reason capture and audit entry

---

## Build Commands

```bash
# Development server
ng serve

# Production build
ng build --configuration production

# Run tests
ng test
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass for `OverrideReasonDialogComponent` (form validation, character counter, role gating)
- [ ] Unit tests pass for `OverrideService` (HTTP calls mocked)
- [ ] Unit tests pass for `OverrideAuditLogComponent` (table rendering with mock data)
- [ ] **[UI Task]** Visual comparison against SCR-027 wireframe at 375px, 768px, 1440px when available
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Override button not rendered for Patient role
- [ ] "Override reason is required" validation shown on empty submit
- [ ] Character counter displays correctly and caps at 500
- [ ] Loading spinner on Confirm button during API call

---

## Implementation Checklist

- [ ] Create `OverrideService` with `submitOverride()` and `getOverrideAuditLog()` methods
- [ ] Create `OverrideReasonDialogComponent` with reactive form, `[Validators.required, Validators.maxLength(500)]`, and `aria-describedby` error association
- [ ] Implement live character counter (`{current}/500`) with warning zone styling at 480+
- [ ] Implement Confirm button loading state (UXR-501) and disabled state when form invalid
- [ ] Implement role-based conditional rendering: override button visible only for Staff/Admin roles
- [ ] Integrate dialog into scheduling flow: open on `409 Conflict` constraint violation, resubmit with override payload on confirmation
- [ ] Create `OverrideAuditLogComponent` with `app-data-table` displaying override events filtered by action type
- [ ] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation
