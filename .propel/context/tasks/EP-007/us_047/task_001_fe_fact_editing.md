---
task_id: task_001
user_story: us_047
epic: EP-007
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_047] Authorized Data Editing and Verification
- **Story Location**: [.propel/context/tasks/EP-007/us_047/us_047.md](.propel/context/tasks/EP-007/us_047/us_047.md)
- **Acceptance Criteria**:
  - AC-1: Given I am an authorized clinician, When I edit an extracted clinical fact, Then the change is saved, the fact's verification state updates to "Verified," and the previous value is recorded in the audit trail.
  - AC-2: Given I verify an extracted fact without changes, When I mark it as "Verified," Then the verification state is updated and the reviewer's identity and timestamp are stored.
  - AC-3: Given an audit trail exists for a clinical fact, When I view the fact's history, Then all previous values, editors, and timestamps are displayed in chronological order.
  - AC-4: Given a patient role attempts to edit a clinical fact, When the edit request is submitted, Then the API returns HTTP 403 and no change is persisted.
- **Edge Cases**:
  - Edge Case 1: Two clinicians edit the same fact simultaneously — optimistic concurrency conflict detected; the second writer receives an HTTP 409 response; a conflict error is shown in the UI with the current value so the user can retry with fresh data.
  - Edge Case 2: Editing a fact referenced by a coding decision — a non-blocking warning is shown inline: "This fact is referenced by a coding decision. Review the coding decision after saving." Edit is allowed to proceed.

---

## Design References (UI Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | `.propel/context/docs/figma_spec.md#SCR-014` |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe — upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-014-patient-profile.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | SCR-014 (360° Patient Profile — Validation state) |
| **UXR Requirements** | UXR-111, UXR-201, UXR-202, UXR-205, UXR-501 |
| **Design Tokens** | Refer to global design tokens; verified badge: green checkmark; AI badge: purple |

> **Note — Screen ID Correction**: US_047 references SCR-025 (Queue Dashboard) in the story file. The correct screen for clinical fact editing and verification is **SCR-014** (360° Patient Profile). SCR-025 belongs to the Staff Queue Dashboard (EP-004). All implementation targets SCR-014 Validation state.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Component Library | Angular Material + CDK | 17.x |
| State/Reactivity | Angular Signals + RxJS | 7.x |
| Forms | Angular Reactive Forms | 17.x |
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

Extend the `ClinicalFactCardComponent` (created in US_045) to support inline editing and one-click verification within the SCR-014 Summary tab (Validation state). Clinicians with write access see an "Edit" pencil icon and a "Verify" checkmark button on each unverified fact card. Clicking "Edit" transitions the card to an inline edit form (Angular Reactive Form) with the current `name` and `value` pre-populated. Submitting the form sends `PATCH /api/v1/clinical-facts/{id}` with an `If-Match` ETag header for optimistic concurrency (Edge Case 1). On HTTP 409 conflict, a non-dismissible inline error shows the current server value and prompts the user to refresh and retry. If the API response includes `referencedByCodingDecision: true`, a non-blocking amber warning banner is displayed: "This fact is referenced by a coding decision. Review the coding decision after saving." (Edge Case 2). Clicking "Verify" (without editing) sends `POST /api/v1/clinical-facts/{id}/verify`; on success, the AI badge transitions to a green verified checkmark (UXR-405, SCR-014 Validation state). The fact card includes a "History" expansion panel that lazy-loads audit entries from `GET /api/v1/clinical-facts/{id}/history` and renders them as a chronological list of previous values, editors, and timestamps (AC-3). All form inputs use `aria-describedby` for error message association (UXR-205). Submit buttons show a loading spinner and are disabled during the network request (UXR-501). WCAG 2.1 AA contrast on edit form and verification state indicators (UXR-201). Full keyboard navigation and visible focus rings (UXR-202). "Edit" and "Verify" actions are only rendered for Clinician role — the Angular authorization check uses the user's role from the auth service (AC-4).

---

## Dependent Tasks

- **us_047/task_002** — `PATCH /api/v1/clinical-facts/{id}`, `POST /api/v1/clinical-facts/{id}/verify`, and `GET /api/v1/clinical-facts/{id}/history` APIs must be implemented.
- **us_045/task_001** — `ClinicalFactCardComponent` base implementation must exist.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ClinicalFactCardComponent` | MODIFY | Add Edit/Verify buttons (Clinician-only), inline edit form, history expansion panel |
| `FactEditFormComponent` | CREATE | Inline Angular Reactive Form: `name` and `value` text inputs; submit/cancel actions |
| `FactHistoryPanelComponent` | CREATE | `mat-expansion-panel` lazy-loading audit history; chronological list of previous values |
| `FactHistoryEntryComponent` | CREATE | Single history row: previous value, editor display name, formatted timestamp |
| `ConcurrencyConflictBannerComponent` | CREATE | Inline error: "Edit conflict — another user changed this fact. Current value: [value]. Refresh and retry." |
| `CodingDecisionWarningComponent` | CREATE | Non-blocking amber inline warning: referenced by coding decision (Edge Case 2) |
| `ClinicalFactService` | MODIFY | Add: `patchFact(id, dto, etag)`, `verifyFact(id)`, `getFactHistory(id)` HttpClient methods |
| `PatientProfileFacade` | MODIFY | On successful edit/verify: update the relevant fact in the local `alerts` signal without full page reload |

---

## Implementation Plan

1. **Add role-gated actions to `ClinicalFactCardComponent`**: Inject `AuthService` to read current user role. Use `@if (canEdit())` computed Signal (`canEdit = computed(() => authService.hasRole('Clinician'))`) to conditionally render the "Edit" (`<mat-icon>edit</mat-icon>`) and "Verify" (`<mat-icon>verified</mat-icon>`) icon buttons. Add `aria-label="Edit fact: {{ fact.name }}"` and `aria-label="Verify fact: {{ fact.name }}"` (UXR-201).
2. **Create `FactEditFormComponent`**: Rendered inline within the card when edit mode is active. Angular Reactive Form with `FormGroup`: `nameControl` (required, max 255 chars), `valueControl` (required). Pre-populate controls from `fact.name` and `fact.value`. Show validation errors (`nameControl.errors`) with `<mat-error id="name-error">` and `aria-describedby="name-error"` on the input (UXR-205). Submit button: apply `[disabled]="form.invalid || saving()"` and show `<mat-spinner diameter="18">` when `saving() === true` (UXR-501). Cancel button resets to view mode without API call.
3. **Wire `FactEditFormComponent` submit**: On submit, set `saving = signal(true)`. Call `ClinicalFactService.patchFact(fact.factId, { name, value }, fact.etag)`. On `HTTP 200`: update the local fact Signal with the returned updated fact (including new ETag), transition card to view mode, show success toast. On `HTTP 409`: show `ConcurrencyConflictBannerComponent` with the server's current value from the response body, set `saving = false`. On other errors: show inline error, set `saving = false`.
4. **Wire "Verify" button**: On click, set `verifying = signal(true)`. Call `ClinicalFactService.verifyFact(fact.factId)`. On `HTTP 200`: update local fact Signal (`verified = true`, `verifiedBy = currentUserId`), transition AI badge to green verified checkmark. Apply `saving` spinner on the Verify button during the request (UXR-501). On error: show inline error toast.
5. **Handle Edge Case 2 — coding decision warning**: When `ClinicalFactService.patchFact()` returns a response with `referencedByCodingDecision: true` (checked before confirming the save, via a pre-check call or returned in the PATCH response), render `CodingDecisionWarningComponent` inline above the form. Display: "This fact is referenced by a coding decision. Review the coding decision after saving." Do not block the save (Edge Case 2).
6. **Create `FactHistoryPanelComponent`**: `<mat-expansion-panel>` with header "History". On panel open (`(opened)` event), if history not yet loaded, call `ClinicalFactService.getFactHistory(fact.factId)` and populate history Signal. Show skeleton loader during fetch. Render `@for (entry of history(); track entry.auditId)` using `FactHistoryEntryComponent`. Show "No edit history" when empty.
7. **Create `FactHistoryEntryComponent`**: Display `previousValue` (highlighted in amber), `editorDisplayName`, and `timestamp` formatted as `'MMM d, yyyy, h:mm a'` via `DatePipe`. Use `<time [dateTime]="entry.timestamp.toISOString()">` for semantic time element (UXR-201).
8. **Accessibility**: All error messages use `aria-describedby` referencing the error element ID (UXR-205). Focus moves to the first invalid field on failed form submit (UXR-202). `FactHistoryPanelComponent` expansion panel has `aria-label="Edit history for {{ fact.name }}"`. Submit and Verify buttons disable and show inline spinner during request (UXR-501). Verify 4.5:1 contrast on verified checkmark green and AI badge purple (UXR-201).

---

## Current Project State

```
src/
├── app/
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   ├── patient-profile/
│   │   │   │   │   ├── patient-profile.component.ts        ← EXISTS (US_045)
│   │   │   │   │   ├── tabs/
│   │   │   │   │   │   └── profile-summary-tab.component.ts  ← EXISTS (US_045)
│   │   │   │   │   └── fact-list/
│   │   │   │   │       ├── fact-list.component.ts            ← EXISTS (US_045)
│   │   │   │   │       └── clinical-fact-card.component.ts   ← MODIFY (add edit/verify/history)
│   │   │   │   └── fact-editing/                           ← CREATE
│   │   │   │       ├── fact-edit-form.component.ts           ← CREATE
│   │   │   │       ├── fact-history-panel.component.ts       ← CREATE
│   │   │   │       ├── fact-history-entry.component.ts       ← CREATE
│   │   │   │       ├── concurrency-conflict-banner.component.ts  ← CREATE
│   │   │   │       └── coding-decision-warning.component.ts  ← CREATE
│   │   │   ├── services/
│   │   │   │   ├── patient-profile.service.ts    ← EXISTS (US_045)
│   │   │   │   └── clinical-fact.service.ts      ← MODIFY (add patchFact, verifyFact, getFactHistory)
│   │   │   └── facades/
│   │   │       └── patient-profile.facade.ts     ← MODIFY (update fact in signal on edit/verify success)
│   │   └── [existing modules...]
│   └── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `app/modules/clinical-intelligence/components/patient-profile/fact-list/clinical-fact-card.component.ts` | Add role-gated Edit/Verify buttons, inline edit mode toggle, history panel, ETag on fact DTO |
| CREATE | `app/modules/clinical-intelligence/components/fact-editing/fact-edit-form.component.ts` | Reactive Form with name/value controls, aria-describedby errors, loading spinner on submit |
| CREATE | `app/modules/clinical-intelligence/components/fact-editing/fact-history-panel.component.ts` | mat-expansion-panel lazy-loading audit history entries |
| CREATE | `app/modules/clinical-intelligence/components/fact-editing/fact-history-entry.component.ts` | Single history row: previous value, editor, formatted timestamp |
| CREATE | `app/modules/clinical-intelligence/components/fact-editing/concurrency-conflict-banner.component.ts` | HTTP 409 conflict error showing current server value and retry prompt |
| CREATE | `app/modules/clinical-intelligence/components/fact-editing/coding-decision-warning.component.ts` | Non-blocking amber warning for facts referenced by coding decisions |
| MODIFY | `app/modules/clinical-intelligence/services/clinical-fact.service.ts` | Add patchFact (with If-Match ETag), verifyFact, getFactHistory HttpClient methods |
| MODIFY | `app/modules/clinical-intelligence/facades/patient-profile.facade.ts` | Update fact in local Signal after successful edit or verify (no full reload) |

---

## External References

- Angular Reactive Forms: https://angular.dev/guide/forms/reactive-forms
- Angular Material Expansion Panel: https://material.angular.io/components/expansion/overview
- HTTP ETag and If-Match: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/If-Match
- WCAG 2.1 AA contrast: https://www.w3.org/TR/WCAG21/#contrast-minimum
- aria-describedby for form errors: https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA21
- FR-CA-004: Authorized staff may edit and verify extracted data with immutable audit history
- NFR-010: Immutable audit evidence for access events and overrides with 7-year retention
- DR-003: Clinical fields must store confidence score, source reference, verification state, and last reviewer metadata
- UXR-111: Confirmation dialog for destructive actions
- UXR-201: WCAG 2.1 AA color contrast
- UXR-202: Full keyboard navigation with visible focus indicators
- UXR-205: Error messages programmatically associated with form fields via aria-describedby
- UXR-501: Submit buttons show loading spinner and disable during network requests
- SCR-014 spec (Validation state): `.propel/context/docs/figma_spec.md#SCR-014`

---

## Build Commands

```bash
# Install dependencies
npm install

# Serve app locally
ng serve

# Run unit tests
ng test --include="**/fact-editing/**" --include="**/clinical-fact-card/**"

# Build production
ng build --configuration=production
```

---

## Implementation Validation Strategy

- [ ] Edit and Verify buttons are visible to Clinician role only; not rendered for Patient/Staff (AC-4)
- [ ] Clicking Edit opens inline form pre-populated with `fact.name` and `fact.value`
- [ ] Submitting edit form sends `PATCH` with `If-Match: {etag}` header
- [ ] On success: card returns to view mode with updated values and green verified checkmark (AC-1)
- [ ] Clicking Verify without editing calls `POST /verify`; card badge transitions to verified checkmark (AC-2)
- [ ] Submit and Verify buttons show loading spinner and are disabled during request (UXR-501)
- [ ] HTTP 409 response: `ConcurrencyConflictBannerComponent` shows current server value (Edge Case 1)
- [ ] `referencedByCodingDecision: true` response: amber warning banner rendered non-blocking (Edge Case 2)
- [ ] History panel opens on expansion; shows previous values, editor names, timestamps in chronological order (AC-3)
- [ ] Form validation errors use `aria-describedby` referencing error element IDs (UXR-205)
- [ ] All interactive elements keyboard-navigable with visible focus rings (UXR-202)
- [ ] 4.5:1 contrast on verified badge (green) and edit form inputs (UXR-201)

---

## Implementation Checklist

- [ ] Modify `ClinicalFactCardComponent`: add Clinician-role-gated Edit/Verify buttons with aria-labels and inline edit mode toggle
- [ ] Create `FactEditFormComponent`: Reactive Form with name/value, aria-describedby error wiring, submit/cancel, loading spinner (UXR-205, UXR-501)
- [ ] Wire edit submit: `PATCH` with `If-Match` ETag; handle 200 (update local Signal), 409 (show conflict banner, Edge Case 1), errors
- [ ] Wire Verify button: `POST /verify`; update local Signal on success; loading spinner during request
- [ ] Create `CodingDecisionWarningComponent`: non-blocking amber warning when `referencedByCodingDecision: true` (Edge Case 2)
- [ ] Create `FactHistoryPanelComponent` + `FactHistoryEntryComponent`: lazy-load history on panel open; chronological list (AC-3)
- [ ] Create `ConcurrencyConflictBannerComponent`: show current server value with retry instruction on HTTP 409
- [ ] Modify `ClinicalFactService`: add `patchFact`, `verifyFact`, `getFactHistory`; modify `PatientProfileFacade` to update Signal on success
