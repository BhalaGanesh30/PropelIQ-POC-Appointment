---
task_id: task_001
user_story: us_037
epic: EP-005
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_037] Insurance Soft Validation Engine
- **Story Location**: [.propel/context/tasks/EP-005/us_037/us_037.md](.propel/context/tasks/EP-005/us_037/us_037.md)
- **Acceptance Criteria**:
  - AC-1: Given I enter insurance details during the booking flow, When I submit the insurance form, Then the system validates the policy number format and provider code against the reference database within 500 ms.
  - AC-2: Given the soft validation detects a format mismatch, When the result is returned, Then a warning indicator is displayed ("Insurance details may be incomplete") but the booking is not blocked.
  - AC-3: Given the insurance details pass soft validation, When the form is submitted, Then a "Verified" status indicator is shown and the record is saved with a `SoftValidated` status flag.
  - AC-4: Given I submit insurance details that completely fail validation, When the result is returned, Then the system flags the record with `ValidationFailed` status and records the validation result for staff review.
- **Edge Cases**:
  - Edge Case 1: Reference database unavailable — validation is skipped; booking proceeds; insurance record saved with `ValidationPending` status and a background retry is queued. Frontend shows informational message.
  - Edge Case 2: Secondary insurance has same policy number as primary — warning flag displayed ("Potential duplicate policy number") but submission is not blocked.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | [.propel/context/wireframes/Hi-Fi/wireframe-SCR-028-insurance-verification.html](.propel/context/wireframes/Hi-Fi/wireframe-SCR-028-insurance-verification.html) |
| **Screen Spec** | [figma_spec.md#SCR-028](.propel/context/docs/figma_spec.md#SCR-028) |
| **UXR Requirements** | UXR-201, UXR-202, UXR-205, UXR-301, UXR-404, UXR-501, UXR-505 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local wireframe file exists at specified path.
>
> **Note**: US_037 references SCR-020 (User Management, EP-003). The insurance validation screen is SCR-028 per figma_spec.md, which is the dedicated Insurance Verification screen under EP-005.

### Screen States (SCR-028)

| State | Description |
|-------|-------------|
| Default | Form for primary insurance (policy number, provider, group), card image upload zones (front/back), secondary insurance toggle |
| Loading | Spinner during validation check and image upload |
| Empty | "No insurance on file" with add CTA |
| Error | Soft validation warnings (non-blocking), upload failure with retry |
| Validation | Format validation results shown inline (pass/warn), verification status badge |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
| Forms | Angular Reactive Forms | 17.x |
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

Implement the `InsuranceValidationFormComponent` as a standalone Angular 17 component for SCR-028. The component renders a single-column form for primary insurance entry (policy number, provider name/code, group number) with card image upload zones (front and back) using the `app-file-upload` design system component. A secondary insurance toggle reveals a duplicate form section. On form submission, the component calls `POST /api/v1/insurance/validate` and displays the validation result inline using colour-coded status indicators per UXR-404: green `app-badge[variant="success"]` for "Verified" (SoftValidated), amber `app-banner[variant="warning"]` for "Insurance details may be incomplete" (format mismatch), and red `app-badge[variant="error"]` for ValidationFailed. Crucially, validation warnings do not block form submission — the booking flow continues regardless of validation outcome (AC-2). When the reference database is unavailable, the form shows an informational message and proceeds with `ValidationPending` status. Duplicate policy number detection between primary and secondary insurance triggers a non-blocking warning. The form uses Angular Reactive Forms with `aria-describedby` for error association (UXR-205), full keyboard navigation (UXR-202), and responsive layout at 375px/768px/1440px breakpoints (UXR-301). Submit buttons show a loading spinner and disable during the network request (UXR-501).

---

## Dependent Tasks

- **us_037/task_002** — `POST /api/v1/insurance/validate` and `POST /api/v1/insurance` endpoints must be deployed (or mocked via Angular HTTP interceptor).
- **us_037/task_003** — `insurance_providers` reference table and `validation_status` enum must exist for the API to function.
- **us_009** — `InsuranceProfile` entity must exist in the database (foundational dependency).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `InsuranceValidationFormComponent` | CREATE | Standalone Angular 17 component — insurance form, validation indicators, card upload |
| `InsuranceValidationFormComponent` (template) | CREATE | Reactive form with policy number, provider, group fields; `app-file-upload` zones; secondary toggle; status badges |
| `InsuranceValidationFormComponent` (styles) | CREATE | Single-column layout, validation indicator colours, responsive breakpoints |
| `InsuranceService` | CREATE | Angular service wrapping `POST /api/v1/insurance/validate` and `POST /api/v1/insurance` |
| `InsuranceValidationResult` model | CREATE | TypeScript interface: `status`, `warnings[]`, `providerMatch`, `policyFormatValid` |
| `InsuranceFormData` model | CREATE | TypeScript interface: `policyNumber`, `providerCode`, `providerName`, `groupNumber`, `tier`, `cardImageFront?`, `cardImageBack?` |
| Patient booking routing | MODIFY | Add insurance step to booking flow referencing `InsuranceValidationFormComponent` |

---

## Implementation Plan

1. **Create `InsuranceService`** in `app/features/insurance/insurance.service.ts`: method `validate(data: InsuranceFormData): Observable<InsuranceValidationResult>` wrapping `HttpClient.post('/api/v1/insurance/validate', data)`. Method `save(data: InsuranceFormData): Observable<InsuranceProfile>` wrapping `HttpClient.post('/api/v1/insurance', data)`.
2. **Create insurance form with Reactive Forms**: Build a `FormGroup` with controls for `policyNumber` (required, minLength 5, maxLength 30), `providerCode` (required), `providerName` (required), `groupNumber` (optional). Add client-side format validators (alphanumeric pattern for policy number). Use `@if` control flow for conditional rendering. Associate error messages with fields via `aria-describedby` (UXR-205).
3. **Implement card image upload zones**: Use `app-file-upload` component for front and back card images. Accept JPEG/PNG up to 5 MB. Show upload progress (UXR-505). Store file references in the form model. Stack zones vertically per SCR-028 layout.
4. **Implement secondary insurance toggle**: `mat-slide-toggle` that reveals a duplicate form section for secondary insurance. On toggle, clone the primary form structure. Compare policy numbers between primary and secondary; if identical, display non-blocking `app-banner[variant="warning"]` "Potential duplicate policy number" (Edge Case 2).
5. **Implement soft validation flow**: On form submit, call `insuranceService.validate()`. During the request, show loading spinner on submit button and disable it (UXR-501). On response, render inline status:
   - `SoftValidated` → green `app-badge[variant="success"]` "Verified"
   - Format mismatch warnings → amber `app-banner[variant="warning"]` "Insurance details may be incomplete" (AC-2, non-blocking)
   - `ValidationFailed` → red `app-badge[variant="error"]` "Validation failed — flagged for staff review" (AC-4)
   - `ValidationPending` (reference DB unavailable) → blue `app-banner[variant="info"]` "Validation pending — booking will continue" (Edge Case 1)
   In all cases, allow the user to proceed with the booking flow. Call `insuranceService.save()` to persist the record.
6. **Implement empty state**: When no insurance is on file, show "No insurance on file" message with "Add Insurance" CTA button per SCR-028.
7. **Integrate into booking flow**: Add the insurance form step to the patient booking route. Ensure the insurance step is optional — the user can skip or complete it without blocking the appointment creation.
8. **Implement responsive layout**: Single-column form at all breakpoints. Card upload zones stacked. Use Angular Material grid directives for consistent spacing. Validate at 375px, 768px, 1440px per UXR-301.

---

## Current Project State

```
app/
├── features/
│   ├── insurance/                                    ← CREATE (this task)
│   │   ├── insurance-validation-form.component.ts
│   │   ├── insurance-validation-form.component.html
│   │   ├── insurance-validation-form.component.scss
│   │   └── insurance.service.ts
│   ├── booking/                                      ← EXISTS (booking flow)
│   │   └── ...                                       ← MODIFY (add insurance step)
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── insurance-validation-result.model.ts      ← CREATE
│       ├── insurance-form-data.model.ts              ← CREATE
│       └── [existing models...]
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual feature folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/insurance/insurance-validation-form.component.ts` | Standalone component: reactive form, validation indicators, card upload, secondary toggle |
| CREATE | `app/features/insurance/insurance-validation-form.component.html` | Template: form fields, `app-file-upload` zones, `app-badge` status, `app-banner` warnings |
| CREATE | `app/features/insurance/insurance-validation-form.component.scss` | Single-column layout, status indicator colours, responsive breakpoints |
| CREATE | `app/features/insurance/insurance.service.ts` | Service wrapping validate POST and save POST endpoints |
| CREATE | `app/shared/models/insurance-validation-result.model.ts` | `InsuranceValidationResult` interface |
| CREATE | `app/shared/models/insurance-form-data.model.ts` | `InsuranceFormData` interface |
| MODIFY | `app/features/booking/[booking-flow].ts` | Add optional insurance step referencing `InsuranceValidationFormComponent` |

---

## External References

- Angular Reactive Forms: https://angular.dev/guide/forms/reactive-forms
- Angular Material Slide Toggle: https://material.angular.io/components/slide-toggle/overview
- WCAG 2.1 AA `aria-describedby` for form errors: https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA1
- FR-IP-001: System MUST perform insurance soft validation against formatting and reference records without blocking booking completion
- UXR-201: WCAG 2.1 AA colour contrast ratio of at least 4.5:1 for normal text
- UXR-202: Full keyboard navigation with visible focus indicators
- UXR-205: Error messages MUST be programmatically associated with form fields using `aria-describedby`
- UXR-301: Mobile (375px), tablet (768px), and desktop (1440px) breakpoints
- UXR-404: Status indicators MUST use consistent colour semantics: green (success), amber (warning), red (error), blue (info)
- UXR-501: Form submission buttons MUST show loading spinner and disable during network requests
- UXR-505: File upload MUST support drag-and-drop with progress bar and cancel capability
- SCR-028 wireframe: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-028-insurance-verification.html`

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

- [ ] Unit tests pass for `InsuranceValidationFormComponent` (form rendering, validation flow, status display, secondary toggle)
- [ ] Unit tests pass for `InsuranceService` (HTTP calls mocked)
- [ ] **[UI Task]** Visual comparison against wireframe at 375px, 768px, 1440px
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment
- [ ] SoftValidated result shows green "Verified" badge (UXR-404)
- [ ] Format mismatch shows amber warning banner without blocking submission (AC-2)
- [ ] ValidationFailed shows red error badge and flags for staff review (AC-4)
- [ ] ValidationPending (reference DB unavailable) shows info banner and allows booking to proceed (Edge Case 1)
- [ ] Duplicate policy number between primary and secondary shows non-blocking warning (Edge Case 2)
- [ ] Submit button shows spinner and disables during validation request (UXR-501)

---

## Implementation Checklist

- [ ] Create `InsuranceService` wrapping `POST /api/v1/insurance/validate` and `POST /api/v1/insurance`
- [ ] Implement reactive form with policy number, provider code, provider name, group number fields and `aria-describedby` error association (UXR-205)
- [ ] Implement card image upload zones (front/back) using `app-file-upload` with drag-and-drop and progress bar (UXR-505)
- [ ] Implement secondary insurance toggle with duplicate policy number warning detection
- [ ] Implement colour-coded validation status display: SoftValidated (green badge), warning (amber banner), ValidationFailed (red badge), ValidationPending (blue banner) per UXR-404
- [ ] Ensure all validation outcomes are non-blocking — booking flow continues regardless of result (AC-2)
- [ ] Integrate insurance form as optional step in booking flow routing
- [ ] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation
