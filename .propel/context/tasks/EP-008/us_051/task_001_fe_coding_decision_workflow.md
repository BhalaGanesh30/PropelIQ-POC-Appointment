---
task_id: task_001
user_story: us_051
epic: EP-008
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_051] Accept, Modify, and Reject Coding Workflow
- **Story Location**: [.propel/context/tasks/EP-008/us_051/us_051.md](.propel/context/tasks/EP-008/us_051/us_051.md)
- **Acceptance Criteria**:
  - AC-1: Given coding suggestions are displayed, When I click "Accept" on a suggestion, Then the code is recorded as the finalized coding decision with my identity, the accepted code, and the AI suggestion it was accepted from stored in the audit trail.
  - AC-2: Given I want to modify a suggestion, When I click "Modify" and update the code or description, Then the modified code is saved as the finalized decision with a "Modified from AI suggestion" audit record including the original and final values.
  - AC-3: Given I reject a suggestion, When I click "Reject" (after confirming the confirmation dialog), Then the suggestion is marked as rejected and I must manually enter a code via the code search workflow.
  - AC-4: Given I have not made a decision on all required codes, When I attempt to submit the encounter for billing, Then the system blocks submission with "Coding decisions required" and lists the pending items.
- **Edge Cases**:
  - Edge Case 1: Edit Decision after accept — an "Edit Decision" button is visible on accepted cards before the encounter is submitted; clicking it re-opens the inline edit form with the accepted code pre-populated.
  - Edge Case 2: Agreement rate tracking — each accept/modify/reject action is recorded by the BE (AIR-007); no additional FE instrumentation required beyond dispatching the correct action verb to the API.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | .propel/context/docs/figma_spec.md#SCR-017 |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-027-coding-suggestions.[html\|png\|jpg]` — **Note: SCR-027 in us_051.md is incorrect. Correct screen is SCR-017 (Coding Suggestion Review). SCR-027 = Staff-Assisted Booking (EP-004 scheduling flow).** |
| **Screen Spec** | SCR-017 Validation state: Accepted codes — green border; Modified codes — editable inline; Rejected codes — grayed with strikethrough. Summary bar at top showing finalization status. |
| **UXR Requirements** | UXR-108, UXR-111, UXR-201, UXR-202, UXR-206, UXR-501 |
| **Design Tokens** | Accepted: green border; Rejected: gray + strikethrough; Modified: editable inline with amber outline; Destructive reject button (UXR-111); `cdkTrapFocus` on confirmation dialog (UXR-206) |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | N/A | N/A |
| ORM | N/A | N/A |
| Database | N/A | N/A |
| Cache | N/A | N/A |
| Observability | N/A | N/A |
| Frontend | Angular + Angular Material + CDK | 17.x |
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

Extend `SuggestionCardComponent` and `CptSuggestionCardComponent` (US_049/US_050) to implement the Accept, Modify, and Reject interaction workflow for SCR-017's Validation state. Each suggestion card transitions from Default state (action buttons visible) to one of three resolved visual states:

- **Accepted** (AC-1): green `mat-card` border; "Accepted" chip badge; action buttons replaced with "Edit Decision" button (Edge Case 1); card becomes non-interactive after acceptance.
- **Modified** (AC-2): inline `mat-form-field` for code editing; amber outline while editing; on save, transitions to Accepted visual state with "Modified from AI" chip badge.
- **Rejected** (AC-3): `RejectConfirmationDialogComponent` opens (`MatDialog`, `disableClose: true`, `cdkTrapFocus`, UXR-111, UXR-206); on confirm — card turns gray, strikethrough text, "Rejected" chip badge; a "Search Code" action link navigates to SCR-018 for manual code entry.

A `CodingDecisionSummaryBarComponent` (summary bar at SCR-017 top) tracks pending, accepted, modified, and rejected counts via Signal state. When any `pending` decisions remain, the summary bar renders a `PendingSubmissionBlockBannerComponent` warning. This same component is rendered at the point of encounter submission trigger (AC-4).

All action buttons use `aria-describedby` pointing to the card's code label (UXR-205); the reject confirmation dialog returns focus to the reject button on close (UXR-206).

---

## Dependent Tasks

- **us_051/task_002** — Provides PATCH endpoints: `accept`, `modify`, `reject`; this FE calls them on each user action.
- **us_049/task_001** — `SuggestionCardComponent` must exist before this task extends it.
- **us_050/task_001** — `CptSuggestionCardComponent` must exist before this task extends it.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `SuggestionCardComponent` | MODIFY | Add Accept/Modify/Reject buttons; visual state transitions (green border, inline edit, gray strikethrough); "Edit Decision" button for accepted cards (Edge Case 1) |
| `CptSuggestionCardComponent` | MODIFY | Same Accept/Modify/Reject interaction as `SuggestionCardComponent`; shared pattern |
| `RejectConfirmationDialogComponent` | CREATE | `MatDialog`; `disableClose: true`; `cdkTrapFocus` (UXR-206); destructive confirm button (UXR-111); on close returns focus to trigger reject button |
| `InlineCodeEditComponent` | CREATE | Reactive `mat-form-field` rendered inside card on Modify click; pre-populated with current code; Save/Cancel buttons; amber outline while editing (AC-2) |
| `CodingDecisionSummaryBarComponent` | CREATE | Top summary bar: pending/accepted/modified/rejected counts via Signal; shows `PendingSubmissionBlockBannerComponent` when pending > 0 (AC-4) |
| `PendingSubmissionBlockBannerComponent` | CREATE | "Coding decisions required — N pending items" banner with list of pending card labels; blocks submission trigger (AC-4) |
| `CodingDecisionFacade` | CREATE | Signal state: `decisions: Map<decisionId, DecisionState>`; tracks per-card state; exposes `pendingCount`, `allDecided` computed signals |
| `CodingDecisionService` | CREATE | HTTP calls: `POST /api/v1/coding-decisions/{id}/accept`, `PATCH /api/v1/coding-decisions/{id}/modify`, `POST /api/v1/coding-decisions/{id}/reject`; maps responses to `DecisionState` |
| `DecisionStateDto` | CREATE | `{ decisionId, action: 'accepted'|'modified'|'rejected', finalCode: string, finalDescription: string }` |

---

## Implementation Plan

1. **Define `DecisionStateDto` and `DecisionState` model**: `DecisionState` type: `'pending' | 'accepted' | 'modified' | 'rejected'`. `DecisionStateDto` carries `decisionId`, `action`, `finalCode`, `finalDescription`. `AcceptRequestDto`: `{ decisionId }`. `ModifyRequestDto`: `{ decisionId, finalCode: string, finalDescription: string }`. `RejectRequestDto`: `{ decisionId }`.
2. **Create `CodingDecisionService`**: Three methods: `accept(decisionId)` → `POST /api/v1/coding-decisions/{decisionId}/accept`; `modify(decisionId, req: ModifyRequestDto)` → `PATCH /api/v1/coding-decisions/{decisionId}/modify`; `reject(decisionId)` → `POST /api/v1/coding-decisions/{decisionId}/reject`. Each returns `Observable<DecisionStateDto>`.
3. **Create `CodingDecisionFacade`**: `decisions = signal<Record<string, DecisionState>>({})`. `pendingCount = computed(() => Object.values(decisions()).filter(s => s === 'pending').length)`. `allDecided = computed(() => pendingCount() === 0)`. Methods: `setDecision(decisionId, state, finalCode, finalDescription)` — updates the signal map. Calls `CodingDecisionService` per action; on success, calls `setDecision`.
4. **Create `InlineCodeEditComponent`**: `@Input() currentCode: string`; `@Input() currentDescription: string`; `@Output() saved = new EventEmitter<{code: string, description: string}>()`; `@Output() cancelled = new EventEmitter<void>()`. Reactive form with `code` (required, max 20 chars) and `description` (required) fields. Amber `mat-form-field` outline styling while active. Save button disabled while form invalid.
5. **Create `RejectConfirmationDialogComponent`**: `MatDialog` with `disableClose: true`; `cdkTrapFocus` applied to dialog container (UXR-206); "Are you sure you want to reject this coding suggestion?" body text; Cancel (secondary) + "Reject" (destructive `mat-button` with `color="warn"`, UXR-111) buttons. On close, emit `confirmed: boolean`; dialog caller returns focus to reject button via `dialogRef.afterClosed()`.
6. **Modify `SuggestionCardComponent`**: Inject `CodingDecisionFacade`. `@switch(facade.decisions()[suggestion.decisionId])`: `pending` → render Accept/Modify/Reject buttons (UXR-108); `accepted` → green border CSS class, "Accepted" `mat-chip`, "Edit Decision" button that sets state back to `editing` (Edge Case 1); `modified` → same as accepted but with "Modified from AI" chip; `rejected` → gray CSS class, strikethrough text, "Rejected" chip, "Search Code" `routerLink` to SCR-018. Accept button calls `facade.accept(id)`; Modify button activates `InlineCodeEditComponent`; Reject button opens `RejectConfirmationDialogComponent`.
7. **Modify `CptSuggestionCardComponent`**: Apply identical interaction pattern as `SuggestionCardComponent` (step 6) for CPT cards using the same `CodingDecisionFacade`.
8. **Create `CodingDecisionSummaryBarComponent` and `PendingSubmissionBlockBannerComponent`**: Summary bar shows counts via `facade.pendingCount()`, `facade.allDecided()`. Renders `PendingSubmissionBlockBannerComponent` (`role="alert"`, `aria-live="assertive"`) when `pendingCount() > 0`, listing pending item codes by name (AC-4). Expose `canSubmit()` computed from `allDecided()` for use by encounter submission trigger.

---

## Current Project State

```
src/
├── app/
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   └── coding-suggestion-panel/
│   │   │   │       ├── coding-suggestion-panel.component.ts    ← EXISTS (US_049/US_050)
│   │   │   │       ├── suggestion-card/
│   │   │   │       │   └── suggestion-card.component.ts        ← MODIFY (add action buttons + state transitions)
│   │   │   │       ├── cpt-suggestion-card/
│   │   │   │       │   └── cpt-suggestion-card.component.ts    ← MODIFY (same pattern as suggestion-card)
│   │   │   │       ├── reject-confirmation-dialog/
│   │   │   │       │   └── reject-confirmation-dialog.component.ts  ← CREATE
│   │   │   │       ├── inline-code-edit/
│   │   │   │       │   └── inline-code-edit.component.ts       ← CREATE
│   │   │   │       ├── coding-decision-summary-bar/
│   │   │   │       │   └── coding-decision-summary-bar.component.ts ← CREATE
│   │   │   │       └── pending-submission-block-banner/
│   │   │   │           └── pending-submission-block-banner.component.ts ← CREATE
│   │   │   ├── facades/
│   │   │   │   ├── coding-suggestion.facade.ts                 ← EXISTS (US_049)
│   │   │   │   ├── cpt-suggestion.facade.ts                    ← EXISTS (US_050)
│   │   │   │   └── coding-decision.facade.ts                   ← CREATE
│   │   │   ├── services/
│   │   │   │   └── coding-decision.service.ts                  ← CREATE
│   │   │   └── models/
│   │   │       └── decision-state.dto.ts                       ← CREATE
│   └── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `modules/clinical-intelligence/components/coding-suggestion-panel/suggestion-card/suggestion-card.component.ts` | Add Accept/Modify/Reject buttons; @switch on DecisionState; Validation state visual transitions; "Edit Decision" for accepted (Edge Case 1) |
| MODIFY | `modules/clinical-intelligence/components/coding-suggestion-panel/cpt-suggestion-card/cpt-suggestion-card.component.ts` | Same Accept/Modify/Reject interaction pattern as SuggestionCardComponent |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/reject-confirmation-dialog/reject-confirmation-dialog.component.ts` | MatDialog; disableClose; cdkTrapFocus (UXR-206); destructive Reject button (UXR-111) |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/inline-code-edit/inline-code-edit.component.ts` | Reactive form for code modification; amber outline; Save/Cancel (AC-2) |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/coding-decision-summary-bar/coding-decision-summary-bar.component.ts` | Summary bar with decision counts; renders PendingSubmissionBlockBannerComponent (AC-4) |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/pending-submission-block-banner/pending-submission-block-banner.component.ts` | role=alert; aria-live=assertive; pending item list (AC-4) |
| CREATE | `modules/clinical-intelligence/facades/coding-decision.facade.ts` | Signal map of decision states; pendingCount computed; allDecided computed |
| CREATE | `modules/clinical-intelligence/services/coding-decision.service.ts` | POST accept, PATCH modify, POST reject endpoints |
| CREATE | `modules/clinical-intelligence/models/decision-state.dto.ts` | DecisionStateDto, AcceptRequestDto, ModifyRequestDto, RejectRequestDto |

---

## External References

- Angular Signals computed: https://angular.dev/guide/signals#computed-signals
- Angular Material Dialog: https://material.angular.io/components/dialog
- Angular CDK FocusTrap: https://material.angular.io/cdk/a11y/overview#focustrap
- Angular Reactive Forms: https://angular.dev/guide/forms/reactive-forms
- UXR-111: Destructive actions require confirmation dialog (reject code)
- UXR-206: Focus trapped in modal dialogs; returned to trigger element on close
- UXR-108: Accept/Modify/Reject buttons anchored at card bottom
- FR-MC-003 [HYBRID]: User decision required before finalization
- NFR-010: Immutable audit evidence for coding decisions (AC-1, AC-2, AC-3)
- AIR-007: Agreement rate tracking — each action verb recorded for monitoring dashboard

---

## Build Commands

```bash
# Install dependencies
npm install

# Build application
ng build --configuration production

# Run unit tests
ng test --watch=false --browsers=ChromeHeadless

# Lint
ng lint

# Serve locally
ng serve
```

---

## Implementation Validation Strategy

- [ ] Accept click → card renders green border, "Accepted" chip, "Edit Decision" button (AC-1, Edge Case 1)
- [ ] Modify click → `InlineCodeEditComponent` renders with current code pre-populated; Save → card transitions to "Modified from AI" chip state (AC-2)
- [ ] Reject click → `RejectConfirmationDialogComponent` opens with `cdkTrapFocus`; focus returns to reject button on cancel (UXR-206); on confirm → card turns gray with strikethrough, "Search Code" link visible (AC-3)
- [ ] `RejectConfirmationDialogComponent` uses destructive "Reject" button (`color="warn"`) and requires explicit confirm (UXR-111)
- [ ] `CodingDecisionSummaryBarComponent` pending count decrements after each decision; `PendingSubmissionBlockBannerComponent` disappears when `allDecided()` is true (AC-4)
- [ ] `PendingSubmissionBlockBannerComponent` lists pending item codes by name with `role="alert"` (AC-4)
- [ ] "Edit Decision" re-opens `InlineCodeEditComponent` with accepted code pre-populated before encounter submission (Edge Case 1)
- [ ] `CodingDecisionFacade` updates Signal state independently per `decisionId`; ICD-10 and CPT decisions tracked in the same map

---

## Implementation Checklist

- [ ] Define `DecisionStateDto`, `AcceptRequestDto`, `ModifyRequestDto`, `RejectRequestDto` DTOs
- [ ] Create `CodingDecisionService`: `accept()`, `modify()`, `reject()` HTTP calls
- [ ] Create `CodingDecisionFacade`: Signal `decisions` map, `pendingCount` and `allDecided` computed signals
- [ ] Create `InlineCodeEditComponent`: Reactive form; amber outline; Save/Cancel; emits `saved` and `cancelled` (AC-2)
- [ ] Create `RejectConfirmationDialogComponent`: `disableClose: true`; `cdkTrapFocus`; destructive Reject button (UXR-111, UXR-206); focus returns to trigger on close
- [ ] Modify `SuggestionCardComponent`: `@switch` on DecisionState; Accept/Modify/Reject buttons; "Edit Decision" for accepted (Edge Case 1); Validation state visual classes
- [ ] Modify `CptSuggestionCardComponent`: same Accept/Modify/Reject interaction pattern
- [ ] Create `CodingDecisionSummaryBarComponent` + `PendingSubmissionBlockBannerComponent`; register `CodingDecisionFacade` + `CodingDecisionService` in module (AC-4)
