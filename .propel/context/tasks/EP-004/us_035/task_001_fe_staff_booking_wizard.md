---
task_id: task_001
user_story: us_035
epic: EP-004
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_035] Staff-Assisted Patient Booking
- **Story Location**: [.propel/context/tasks/EP-004/us_035/us_035.md](.propel/context/tasks/EP-004/us_035/us_035.md)
- **Acceptance Criteria**:
  - AC-1: Staff searches for a patient by name or phone, selects a slot, and a booking is created without patient-side verification.
  - AC-2: Patient receives standard confirmation email and ICS artifacts; booking is attributed to the staff member who created it.
  - AC-3: Staff can create a basic patient profile inline when the patient does not yet have an account.
  - AC-4: Audit log shows the booking was created by a staff actor on behalf of the patient.
- **Edge Cases**:
  - Edge Case 1: Patient has a conflicting appointment at the same time; conflict warning shown with existing appointment details; staff can proceed with override acknowledgment.
  - Edge Case 2: Staff cannot book for themselves via the staff-assisted flow; booking is for other patients only.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | [.propel/context/wireframes/Hi-Fi/wireframe-SCR-027-staff-assisted-booking.html](.propel/context/wireframes/Hi-Fi/wireframe-SCR-027-staff-assisted-booking.html) |
| **Screen Spec** | [figma_spec.md#SCR-027](.propel/context/docs/figma_spec.md#SCR-027) |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-501 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local wireframe file exists at specified path.
>
> **Note**: US_035 references SCR-018 (Code Search, EP-008). The staff-assisted booking flow is SCR-027 per figma_spec.md, which is the dedicated Staff-Assisted Booking screen under EP-004.

### Screen States (SCR-027)

| State | Description |
|-------|-------------|
| Default | Multi-step wizard: Step 1 Patient Select (search/create), Step 2 Slot Pick, Step 3 Intake/Override, Step 4 Confirm |
| Loading | Spinner during patient search and booking creation (UXR-501) |
| Empty | Patient search prompt: "Search for a patient by name or phone number" |
| Error | Booking failure with reason, override validation error, conflict warning with existing appointment details |
| Validation | Override reason required if scheduling constraint bypassed, booking confirmation toast |

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

Implement the `StaffBookingWizardComponent` as a standalone Angular 17 multi-step wizard component for SCR-027. The wizard guides staff through four sequential steps: (1) Patient Select — search existing patients by name or phone with debounced typeahead using `GET /api/v1/patients/search`, or create a new patient profile inline; (2) Slot Pick — search available appointment slots using `GET /api/v1/appointments/slots` and select one; (3) Intake/Override — simplified intake form with visit reason, and an optional override reason field if the booking conflicts with a scheduling constraint; (4) Confirm — review summary and submit via `POST /api/v1/staff-bookings`. The wizard uses the `app-steps` horizontal step indicator from the design system. A conflict warning banner appears in Step 2 if the selected slot overlaps with an existing patient appointment. The flow prevents self-booking by filtering out the logged-in staff member's own patient ID from the patient search results. On successful booking, the confirmation screen (SCR-006) displays with standard artifacts.

---

## Dependent Tasks

- **us_035/task_002** — `POST /api/v1/staff-bookings` and slot/patient search endpoints must be deployed (or mocked via Angular HTTP interceptor) for end-to-end validation.
- **us_033/task_001** — `PatientSearchService` is reused for patient search by name or phone (shared service).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `StaffBookingWizardComponent` | CREATE | Standalone Angular 17 component — multi-step wizard with 4 steps |
| `StaffBookingWizardComponent` (template) | CREATE | Uses `app-steps`, `app-search-input`, `app-input`, `app-select`, `app-button`, `app-banner` |
| `StaffBookingWizardComponent` (styles) | CREATE | Wizard layout, step transitions, conflict warning banner styles |
| `StaffBookingService` | CREATE | Angular service wrapping `POST /api/v1/staff-bookings` |
| `SlotSearchService` | CREATE | Angular service wrapping `GET /api/v1/appointments/slots` with date/duration/type params |
| `StaffBookingRequest` model | CREATE | TypeScript interface: `patientId`, `slotId`, `visitReason`, `overrideReason?`, `newPatient?` |
| `StaffBookingResponse` model | CREATE | TypeScript interface: `bookingId`, `appointmentId`, `confirmationUrl`, `staffActorId` |
| `SlotResult` model | CREATE | TypeScript interface: `slotId`, `dateTime`, `duration`, `available`, `conflictDetails?` |
| `InlinePatientForm` model | CREATE | TypeScript interface: `firstName`, `lastName`, `phone`, `dateOfBirth`, `email?` |
| Staff routing module | MODIFY | Register `/staff/booking` route pointing to `StaffBookingWizardComponent` |

---

## Implementation Plan

1. **Create `StaffBookingService`** in `app/features/staff-booking/staff-booking.service.ts`: method `createBooking(payload: StaffBookingRequest): Observable<StaffBookingResponse>` wrapping `HttpClient.post('/api/v1/staff-bookings', payload)`.
2. **Create `SlotSearchService`** in `app/features/staff-booking/slot-search.service.ts`: method `searchSlots(date: string, duration: number, type?: string): Observable<SlotResult[]>` wrapping `HttpClient.get('/api/v1/appointments/slots', { params })`. Method `checkConflict(patientId: string, slotId: string): Observable<ConflictCheck>` wrapping `GET /api/v1/appointments/conflict-check`.
3. **Create `StaffBookingWizardComponent`** with `app-steps[steps]` for the 4-step horizontal indicator. Manage wizard state via a `currentStep` signal (Angular 17). Each step is a `@switch` block rendering the appropriate step content.
4. **Step 1 — Patient Select**: Reuse `PatientSearchService` (from us_033) with debounced typeahead (`debounceTime(300)`, `switchMap`). Display results in a list with name, DOB, phone. Include "Create New Patient" option that expands an inline form with `firstName`, `lastName`, `phone`, `dateOfBirth`, `email` fields. Filter out the logged-in staff user's own patient ID to prevent self-booking.
5. **Step 2 — Slot Pick**: Render a date picker and duration selector. On date selection, call `slotSearchService.searchSlots()` and display available slots in a grid/list. After slot selection, if a `patientId` is set, call `slotSearchService.checkConflict()`. If conflict detected, show `app-banner[variant="warning"]` with existing appointment details and "Proceed with Override" option.
6. **Step 3 — Intake/Override**: Reactive form with `visitReason` (required, max 500 chars). If conflict was detected in Step 2, show `overrideReason` textarea (required, max 300 chars per figma_spec field lengths) with character counter. Auto-populate any known patient intake data.
7. **Step 4 — Confirm**: Display a read-only summary of selected patient, slot, reason, and override (if any). Confirm button with loading state (UXR-501). On submit, call `staffBookingService.createBooking(payload)`. On success, navigate to booking confirmation (SCR-006) with the booking ID. On error, show `app-toast[error]` with server message.
8. **Register route**: Add `{ path: 'staff/booking', component: StaffBookingWizardComponent }` to the staff routing module. Add navigation link in the staff dashboard sidebar.

---

## Current Project State

```
app/
├── features/
│   ├── staff-booking/                                ← CREATE (this task)
│   │   ├── staff-booking-wizard.component.ts
│   │   ├── staff-booking-wizard.component.html
│   │   ├── staff-booking-wizard.component.scss
│   │   ├── staff-booking.service.ts
│   │   └── slot-search.service.ts
│   ├── walkin/                                       ← EXISTS (us_033)
│   │   ├── patient-search.service.ts                ← REUSE
│   │   └── ...
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── staff-booking-request.model.ts            ← CREATE
│       ├── staff-booking-response.model.ts           ← CREATE
│       ├── slot-result.model.ts                      ← CREATE
│       └── inline-patient-form.model.ts              ← CREATE
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual feature folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/staff-booking/staff-booking-wizard.component.ts` | Multi-step wizard: patient select, slot pick, intake/override, confirm |
| CREATE | `app/features/staff-booking/staff-booking-wizard.component.html` | Template: `app-steps`, search, slot grid, intake form, summary |
| CREATE | `app/features/staff-booking/staff-booking-wizard.component.scss` | Wizard layout, step transitions, conflict banner styles |
| CREATE | `app/features/staff-booking/staff-booking.service.ts` | Service wrapping `POST /api/v1/staff-bookings` |
| CREATE | `app/features/staff-booking/slot-search.service.ts` | Service wrapping slot search and conflict check endpoints |
| CREATE | `app/shared/models/staff-booking-request.model.ts` | `StaffBookingRequest` and `InlinePatientForm` interfaces |
| CREATE | `app/shared/models/staff-booking-response.model.ts` | `StaffBookingResponse` interface |
| CREATE | `app/shared/models/slot-result.model.ts` | `SlotResult` and `ConflictCheck` interfaces |
| MODIFY | `app/app.routes.ts` (or staff routing) | Add `{ path: 'staff/booking', component: StaffBookingWizardComponent }` |

---

## External References

- Angular 17 control flow (`@switch`, `@if`): https://angular.dev/guide/templates/control-flow
- Angular 17 Signals: https://angular.dev/guide/signals
- Angular 17 Reactive Forms: https://angular.dev/guide/forms/reactive-forms
- RxJS `debounceTime` + `switchMap` typeahead pattern: https://rxjs.dev/api/operators/debounceTime
- `aria-describedby` for inline validation (UXR-205): https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA1
- FR-SO-005: Staff create bookings on behalf of patients without patient-side verification
- SCR-027 wireframe: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-027-staff-assisted-booking.html`

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

- [ ] Unit tests pass for `StaffBookingWizardComponent` (wizard navigation, form validation, conflict detection)
- [ ] Unit tests pass for `StaffBookingService` and `SlotSearchService` (HTTP calls mocked)
- [ ] **[UI Task]** Visual comparison against wireframe at 375px, 768px, 1440px
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Self-booking prevention verified: logged-in staff user's patient ID excluded from search results
- [ ] Conflict warning banner displayed when slot overlaps existing patient appointment
- [ ] Loading spinner on Confirm button during API call (UXR-501)
- [ ] Inline patient creation form validates required fields before proceeding

---

## Implementation Checklist

- [ ] Create `StaffBookingService` wrapping `POST /api/v1/staff-bookings`
- [ ] Create `SlotSearchService` wrapping slot search and conflict check endpoints
- [ ] Implement Step 1 (Patient Select): reuse `PatientSearchService`, add inline patient creation form, filter self-booking
- [ ] Implement Step 2 (Slot Pick): date picker, slot grid, conflict check with warning banner
- [ ] Implement Step 3 (Intake/Override): visit reason form with optional override reason and character counter
- [ ] Implement Step 4 (Confirm): read-only summary, Confirm button with loading state, navigation to SCR-006 on success
- [ ] Register `/staff/booking` route and add navigation link in staff sidebar
- [ ] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation
