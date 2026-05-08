---
task_id: task_001
user_story: us_033
epic: EP-004
layer: Frontend
status: not-started
effort_hours: 6
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_033] Walk-In Creation and Patient Registration Conversion
- **Story Location**: [.propel/context/tasks/EP-004/us_033/us_033.md](.propel/context/tasks/EP-004/us_033/us_033.md)
- **Acceptance Criteria**:
  - AC-1: Staff creates a walk-in entry with patient name and visit reason; entry is inserted into the queue with an estimated wait-time position.
  - AC-2: Staff initiates patient registration for a walk-in; new patient account created and walk-in record associated.
  - AC-3: Walk-in appears on queue dashboard with a "Walk-In" label distinguishing it from scheduled patients.
  - AC-4: Existing patient search by name or phone number finds the profile; walk-in created against existing account without duplication.
- **Edge Cases**:
  - Edge Case 1: Multiple patients match search → disambiguation list shown with demographics (name, DOB, phone).
  - Edge Case 2: Clinic at maximum capacity → visual capacity warning displayed; walk-in creation still allowed.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-029-walk-in-registration.[html\|png\|jpg]` or provide external URL |
| **Screen Spec** | [figma_spec.md#SCR-029](.propel/context/docs/figma_spec.md#SCR-029) |
| **UXR Requirements** | UXR-201, UXR-202, UXR-205, UXR-301, UXR-501 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **PENDING**: UI-impacting task awaiting wireframe — provide file or URL before pixel-perfect validation.
>
> **Note**: us_033 references SCR-016 (Conflict Alerts). Corrected to SCR-029 (Walk-in Registration) per figma_spec.md — SCR-029 is the dedicated walk-in quick-entry form screen under EP-004.

### Screen States (SCR-029)

| State | Description |
|-------|-------------|
| Default | Minimal form: name, phone, reason. Add-to-queue CTA. Convert-to-patient toggle. |
| Loading | Spinner on queue insertion and patient search (UXR-501) |
| Empty | N/A — form always shows input fields |
| Error | Validation errors inline (`aria-describedby` per UXR-205), queue insertion failure toast |
| Validation | Success toast with queue position number, patient conversion confirmation |

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

Implement the `WalkinRegistrationComponent` as a standalone Angular 17 component for SCR-029. The component provides a compact single-column form (max-width 480px) with fields for patient name, phone number, and visit reason. It includes a debounced patient search input (`app-search-input`) that calls `GET /api/v1/patients/search` as the user types. When matches are found, a disambiguation list renders below the search field showing name, DOB, and phone for each result. If the user selects an existing patient, the walk-in is linked to that account. If no match or "New Patient" is chosen, the walk-in creates a temporary record. A "Convert to Patient" toggle enables inline registration fields. On submit, the form calls `POST /api/v1/walkins` and shows a success toast with the queue position. A capacity warning `app-banner[variant="warning"]` is shown when the backend reports the clinic is at capacity.

---

## Dependent Tasks

- **us_031 task_001** — `QueueDashboardComponent` must exist; the walk-in "Add to Queue" CTA on SCR-025 (Empty state) links to this form.
- **task_002** (us_033) — `POST /api/v1/walkins` and `GET /api/v1/patients/search` endpoints must be deployed (or mocked via Angular HTTP interceptor) for end-to-end validation.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `WalkinRegistrationComponent` | CREATE | Standalone Angular component — form, search, disambiguation, conversion |
| `WalkinRegistrationComponent` (template) | CREATE | Reactive form with `app-input`, `app-search-input`, `app-select`, `app-button`, `app-banner` |
| `WalkinRegistrationComponent` (styles) | CREATE | Compact form layout (max-width: 480px), capacity warning banner styles |
| `WalkinService` | CREATE | Angular service wrapping `POST /api/v1/walkins` |
| `PatientSearchService` | CREATE | Angular service wrapping `GET /api/v1/patients/search` with debounce |
| `WalkinEntry` model | CREATE | TypeScript interface for walk-in DTO |
| `PatientSearchResult` model | CREATE | TypeScript interface for search result DTO |
| Staff routing module | MODIFY | Register `/staff/walkin` route |
| `QueueDashboardComponent` | MODIFY | Add "Walk-In" badge variant to distinguish walk-in entries in the queue table |

---

## Implementation Plan

1. **Create `WalkinService`** in `app/features/walkin/walkin.service.ts`: method `createWalkin(payload: CreateWalkinRequest): Observable<WalkinResponse>` wrapping `HttpClient.post('/api/v1/walkins', payload)`.
2. **Create `PatientSearchService`** in `app/features/walkin/patient-search.service.ts`: method `search(query: string): Observable<PatientSearchResult[]>` wrapping `HttpClient.get('/api/v1/patients/search', { params: { q: query } })`.
3. **Create reactive form** using Angular `FormBuilder` with validators: `name` (required, max 200 chars), `phone` (optional, pattern `/^\+?[0-9\s\-()]{7,20}$/`), `visitReason` (required, max 500 chars), `existingPatientId` (optional UUID), `convertToPatient` (boolean toggle, default `false`).
4. **Implement debounced patient search**: On `name` field value changes, apply `debounceTime(300)` + `distinctUntilChanged()` + `filter(q => q.length >= 2)` + `switchMap(q => patientSearchService.search(q))`. Display results in a dropdown list with name, DOB, phone columns.
5. **Implement disambiguation list**: When search returns multiple results, render a list below the input using `@for`. Each row shows patient name, DOB, phone. Clicking a row sets `existingPatientId` and autofills the name field.
6. **Implement "Convert to Patient" toggle**: When enabled, show additional registration fields (DOB, email) below the main form. These are submitted alongside the walk-in creation payload.
7. **Implement capacity warning**: After form renders, check `GET /api/v1/queue/today` response's `totalCount` against a configurable threshold. If at capacity, display `app-banner[variant="warning"]` with "Clinic at capacity. Walk-in creation is still permitted."
8. **Implement submit handler**: On form submit, set button loading state (UXR-501), call `walkinService.createWalkin(payload)`. On success, show `app-toast[success]` "Walk-in added — Queue position #N" and navigate to `/staff/queue`. On error, show `app-toast[error]` with server message.

---

## Current Project State

```
app/
├── features/
│   ├── queue/                                ← EXISTS (us_031 task_001)
│   │   ├── queue-dashboard.component.*      ← MODIFY (add Walk-In badge variant)
│   │   └── ...
│   └── walkin/                               ← CREATE (this task)
│       ├── walkin-registration.component.ts
│       ├── walkin-registration.component.html
│       ├── walkin-registration.component.scss
│       ├── walkin.service.ts
│       └── patient-search.service.ts
├── shared/
│   └── models/
│       ├── walkin-entry.model.ts             ← CREATE
│       └── patient-search-result.model.ts    ← CREATE
└── [existing app structure...]
```

> Placeholder: Update this tree after us_031 task_001 is complete and the actual queue feature folder is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/walkin/walkin-registration.component.ts` | Standalone Angular component — reactive form, patient search, capacity check |
| CREATE | `app/features/walkin/walkin-registration.component.html` | Template — form fields, disambiguation list, convert toggle, capacity banner |
| CREATE | `app/features/walkin/walkin-registration.component.scss` | Compact form layout (max-width: 480px), disambiguation list styles |
| CREATE | `app/features/walkin/walkin.service.ts` | Service wrapping `POST /api/v1/walkins` |
| CREATE | `app/features/walkin/patient-search.service.ts` | Debounced service wrapping `GET /api/v1/patients/search` |
| CREATE | `app/shared/models/walkin-entry.model.ts` | `WalkinEntry`, `CreateWalkinRequest`, `WalkinResponse` interfaces |
| CREATE | `app/shared/models/patient-search-result.model.ts` | `PatientSearchResult` interface (id, name, dob, phone) |
| MODIFY | `app/app.routes.ts` (or staff routing) | Add `{ path: 'staff/walkin', component: WalkinRegistrationComponent }` |
| MODIFY | `app/features/queue/queue-dashboard.component.html` | Add "Walk-In" `app-badge[variant="neutral"]` label when entry is a walk-in |

---

## External References

- Angular 17 Reactive Forms: https://angular.dev/guide/forms/reactive-forms
- RxJS `debounceTime` + `switchMap` typeahead pattern: https://rxjs.dev/api/operators/debounceTime
- Angular `@for` control flow (Angular 17): https://angular.dev/guide/templates/control-flow
- `aria-describedby` for inline validation (UXR-205): https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA1
- FR-SO-003: Walk-in creation, queue insertion, and conversion of walk-ins to registered patients

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

- [ ] Unit tests pass for `WalkinRegistrationComponent` (form validation, search trigger, disambiguation rendering)
- [ ] Unit tests pass for `PatientSearchService` (debounce timing, query param format)
- [ ] Walk-in form submits successfully and shows success toast with queue position
- [ ] Debounced patient search triggers after 300ms pause with 2+ characters
- [ ] Disambiguation list renders when multiple patients match; selecting a row populates `existingPatientId`
- [ ] "Convert to Patient" toggle shows/hides additional registration fields
- [ ] Capacity warning banner shown when queue is at capacity threshold
- [ ] Submit button shows loading spinner and disables during request (UXR-501)
- [ ] Inline validation errors use `aria-describedby` for screen reader association (UXR-205)
- [ ] Walk-in entries on queue dashboard display "Walk-In" label (AC-3)
- [ ] **[UI Task]** Visual comparison against SCR-029 wireframe at 375px, 768px, 1440px when available
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment once wireframe uploaded

---

## Implementation Checklist

- [ ] Create `WalkinService` with `createWalkin()` method wrapping `POST /api/v1/walkins`
- [ ] Create `PatientSearchService` with debounced `search()` method wrapping `GET /api/v1/patients/search`
- [ ] Create `WalkinRegistrationComponent` with reactive form (name, phone, visitReason, convertToPatient toggle)
- [ ] Implement debounced patient search on name field with disambiguation list rendering
- [ ] Implement "Convert to Patient" toggle revealing additional registration fields (DOB, email)
- [ ] Implement capacity warning banner using queue total count vs. configurable threshold
- [ ] Implement submit handler with loading state (UXR-501) and success/error toast
- [ ] Add "Walk-In" badge variant to `QueueDashboardComponent` table row for walk-in entries (AC-3)
