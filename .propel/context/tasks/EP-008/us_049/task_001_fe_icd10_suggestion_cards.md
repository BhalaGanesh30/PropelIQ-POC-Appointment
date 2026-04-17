---
task_id: task_001
user_story: us_049
epic: EP-008
layer: Frontend
status: not-started
effort_hours: 8
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_049] ICD-10 Code Suggestion Generation
- **Story Location**: [.propel/context/tasks/EP-008/us_049/us_049.md](.propel/context/tasks/EP-008/us_049/us_049.md)
- **Acceptance Criteria**:
  - AC-1: Given a patient's clinical profile is available, When I request ICD-10 suggestions, Then the system returns the top 3 ranked ICD-10 codes with confidence scores and a rationale text linked to specific extracted clinical facts within 2.5 seconds p95.
  - AC-2: Given suggestions are returned, When I view the suggestion panel, Then each suggestion displays the ICD-10 code, description, confidence score, and a "View Evidence" link that opens the supporting clinical facts.
  - AC-3: Given the AI model confidence is below the configured threshold, When suggestions are generated, Then the system flags the result as low-confidence and prominently displays "Manual review recommended" before presenting the suggestions.
  - AC-4: Given the suggestion API is called, When the output schema is validated, Then at least 99% of responses pass schema validation with all required fields present.
- **Edge Cases**:
  - Edge Case 1: Fewer than 3 codes returned — render available cards (1 or 2) with an informational note "Insufficient evidence for a third suggestion" below the section.
  - Edge Case 2: No extracted clinical facts — API returns HTTP 422; FE displays Empty state: "No suggestions available — manual coding required" with a code search link (navigates to SCR-018).

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | .propel/context/docs/figma_spec.md#SCR-017 |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-027-coding-suggestions.[html\|png\|jpg]` — **Note: SCR-027 in us_049.md is incorrect. Correct screen is SCR-017 (Coding Suggestion Review). SCR-027 = Staff-Assisted Booking (EP-004 scheduling flow).** |
| **Screen Spec** | SCR-017: Two-section layout (ICD-10 top, CPT below). Suggestion cards stacked vertically. Action buttons at card bottom. Summary bar at top. |
| **UXR Requirements** | UXR-108, UXR-201, UXR-202, UXR-301, UXR-405, UXR-501, UXR-111 |
| **Design Tokens** | AI marker badge (UXR-405), confidence bar, monospace JetBrains Mono for code display, destructive reject button (UXR-111) |

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

Implement the ICD-10 suggestion panel UI for SCR-017 (Coding Suggestion Review). The panel displays up to 3 AI-generated ICD-10 suggestion cards, each showing: code badge (monospace JetBrains Mono per design tokens), description, confidence score as a progress bar with numeric label, rationale text, and a "View Evidence" link. An AI-generated content badge (UXR-405: labeled badge or distinct background tint) is applied to every suggestion card to visually distinguish AI content from user-entered data.

When the API returns `lowConfidence: true` (AC-3), a prominent amber banner "Manual review recommended" is rendered above the suggestion list using an `aria-live="polite"` region. The "View Evidence" link (AC-2) opens a `MatBottomSheet` listing the supporting `ClinicalFactDto` items retrieved as citations from the suggestion response.

States to implement per SCR-017: Default (cards rendered), Loading (skeleton cards during AI inference, max 2.5s), Empty (HTTP 422 — "No suggestions available — manual coding required" with SCR-018 navigation link), and Error (AI service failure banner with retry and fallback to manual). The component exposes a Signal-based facade for state management.

---

## Dependent Tasks

- **us_049/task_002** — Provides `GET /api/v1/patients/{id}/coding-suggestions` BE endpoint that this FE consumes.
- **us_044/task_001** — `clinical_facts` must be extracted and stored for the AI pipeline to produce evidence citations.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `CodingSuggestionPanelComponent` | CREATE | Host container for ICD-10 section; loads suggestions on init; manages Default/Loading/Empty/Error states |
| `SuggestionCardComponent` | CREATE | Individual suggestion card: code badge (monospace), description, confidence progress-bar, rationale text, AI badge (UXR-405) |
| `EvidenceBottomSheetComponent` | CREATE | `MatBottomSheet` listing supporting `ClinicalFactDto` citation items for a selected suggestion; `cdkTrapFocus` |
| `LowConfidenceBannerComponent` | CREATE | Amber `mat-card` banner; rendered when API `lowConfidence: true`; `aria-live="polite"` region (AC-3) |
| `InsufficientEvidenceNoteComponent` | CREATE | Informational note rendered below cards when fewer than 3 returned (Edge Case 1) |
| `CodingSuggestionFacade` | CREATE | Signal-based state: `suggestions`, `loadingState`, `lowConfidence`; calls `CodingSuggestionService` |
| `CodingSuggestionService` | CREATE | HTTP service: `GET /api/v1/patients/{id}/coding-suggestions`; maps `CodingSuggestionResponseDto` |
| `CodingSuggestionResponseDto` | CREATE | `{ suggestions: IcdSuggestionDto[], lowConfidence: boolean, insufficientEvidence: boolean }` |
| `IcdSuggestionDto` | CREATE | `{ decisionId, icdCode, description, confidence, rationale, citations: ClinicalFactCitationDto[] }` |
| SCR-017 routing | MODIFY | Register `CodingSuggestionPanelComponent` route under `ClinicalIntelligence` lazy module |

---

## Implementation Plan

1. **Define DTOs**: `IcdSuggestionDto` (`decisionId: string`, `icdCode: string`, `description: string`, `confidence: number` 0–1, `rationale: string`, `citations: ClinicalFactCitationDto[]`); `ClinicalFactCitationDto` (`factId`, `factType`, `name`, `value`, `factDate`); `CodingSuggestionResponseDto` (`suggestions: IcdSuggestionDto[]`, `lowConfidence: boolean`, `insufficientEvidence: boolean`).
2. **Create `CodingSuggestionService`**: `getSuggestions(patientId: string): Observable<CodingSuggestionResponseDto>` — calls `GET /api/v1/patients/{patientId}/coding-suggestions`; maps HTTP 422 to empty-state signal (Edge Case 2) via `catchError`.
3. **Create `CodingSuggestionFacade`**: `loadingState = signal<'idle'|'loading'|'loaded'|'error'|'empty'>('idle')`; `suggestions = signal<IcdSuggestionDto[]>([])`; `lowConfidence = signal(false)`. `loadSuggestions(patientId)` sets loading → calls service → sets loaded/empty/error.
4. **Create `SuggestionCardComponent`**: `@Input() suggestion: IcdSuggestionDto`. Code badge: `<span class="code-badge font-mono">{{ suggestion.icdCode }}</span>`. Confidence bar: `<mat-progress-bar mode="determinate" [value]="suggestion.confidence * 100">`. Rationale block: expandable `<p>`. AI badge per UXR-405: `<span class="ai-badge">AI-generated</span>` with `background: var(--color-ai-tint)`. "View Evidence" button: emits `(viewEvidence)` output; disabled when `citations.length === 0`.
5. **Create `EvidenceBottomSheetComponent`**: `MAT_BOTTOM_SHEET_DATA` injection with `citations: ClinicalFactCitationDto[]`. Renders a `<mat-list>` of citation items (fact type, name, value, date). `cdkTrapFocus`; accessible close button.
6. **Create `LowConfidenceBannerComponent`**: Amber `mat-card` with warning icon; text "Manual review recommended — AI confidence is below the minimum threshold."; `role="status"` + `aria-live="polite"` (AC-3). Shown conditionally via `@if(facade.lowConfidence())`.
7. **Create `CodingSuggestionPanelComponent`**: `@if`/`@switch` on `facade.loadingState()`. Loading → `@for(i of 3)` `mat-card` skeleton. Empty (HTTP 422) → `InsufficientEvidenceNoteComponent` + link to SCR-018 (Edge Case 2). Error → retry button calls `facade.loadSuggestions(patientId)`. Loaded → optional `LowConfidenceBannerComponent` + `@for(s of facade.suggestions())` `SuggestionCardComponent` + optional `InsufficientEvidenceNoteComponent` when `suggestions.length < 3` (Edge Case 1). "View Evidence" handler opens `MatBottomSheet`.
8. **Add routing and module registration**: Lazy-load `CodingSuggestionPanelComponent` within `ClinicalIntelligenceModule`; register `CodingSuggestionFacade` and `CodingSuggestionService` in module providers.

---

## Current Project State

```
src/
├── app/
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   ├── patient-profile/                       ← EXISTS (US_045)
│   │   │   │   ├── conflict-alerts/                       ← EXISTS (US_046)
│   │   │   │   ├── clinical-facts/                        ← EXISTS (US_047)
│   │   │   │   ├── clinical-timeline/                     ← EXISTS (US_048)
│   │   │   │   └── coding-suggestion-panel/               ← CREATE
│   │   │   │       ├── coding-suggestion-panel.component.ts
│   │   │   │       ├── suggestion-card/
│   │   │   │       │   └── suggestion-card.component.ts
│   │   │   │       ├── evidence-bottom-sheet/
│   │   │   │       │   └── evidence-bottom-sheet.component.ts
│   │   │   │       ├── low-confidence-banner/
│   │   │   │       │   └── low-confidence-banner.component.ts
│   │   │   │       └── insufficient-evidence-note/
│   │   │   │           └── insufficient-evidence-note.component.ts
│   │   │   ├── facades/
│   │   │   │   └── coding-suggestion.facade.ts            ← CREATE
│   │   │   ├── services/
│   │   │   │   └── coding-suggestion.service.ts           ← CREATE
│   │   │   └── models/
│   │   │       └── coding-suggestion.dto.ts               ← CREATE
│   └── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/coding-suggestion-panel.component.ts` | Host container; Default/Loading/Empty/Error state routing via `@switch` |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/suggestion-card/suggestion-card.component.ts` | Card: code badge (monospace), description, confidence bar, rationale, AI badge (UXR-405), View Evidence button |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/evidence-bottom-sheet/evidence-bottom-sheet.component.ts` | MatBottomSheet citations list; cdkTrapFocus; close button |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/low-confidence-banner/low-confidence-banner.component.ts` | Amber banner; role=status; aria-live=polite (AC-3) |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/insufficient-evidence-note/insufficient-evidence-note.component.ts` | Informational note for < 3 suggestions (Edge Case 1) |
| CREATE | `modules/clinical-intelligence/facades/coding-suggestion.facade.ts` | Signal state: suggestions, loadingState, lowConfidence |
| CREATE | `modules/clinical-intelligence/services/coding-suggestion.service.ts` | GET /api/v1/patients/{id}/coding-suggestions; HTTP 422 → empty signal |
| CREATE | `modules/clinical-intelligence/models/coding-suggestion.dto.ts` | IcdSuggestionDto, ClinicalFactCitationDto, CodingSuggestionResponseDto |
| MODIFY | `modules/clinical-intelligence/clinical-intelligence.module.ts` | Register new components, facade, service; add route for SCR-017 |

---

## External References

- Angular Signals: https://angular.dev/guide/signals
- Angular Material Progress Bar: https://material.angular.io/components/progress-bar
- Angular Material Bottom Sheet: https://material.angular.io/components/bottom-sheet
- CDK FocusTrap: https://material.angular.io/cdk/a11y/overview#focustrap
- UXR-405: AI-generated content badge / background tint
- UXR-108: Suggestion card with code badge, confidence, rationale, action buttons
- UXR-111: Destructive button (reject); confirm dialog pattern
- AIR-005: Fallback to manual coding when confidence below threshold (AC-3)
- FR-MC-001: Top-3 ICD-10 with confidence and explainable rationale
- SCR-017: Coding Suggestion Review — ICD-10 section with suggestion cards

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

- [ ] `CodingSuggestionPanelComponent` renders up to 3 `SuggestionCardComponent` items in Default state (AC-1)
- [ ] Each `SuggestionCardComponent` shows: ICD-10 code (monospace badge), description, confidence progress bar, rationale text, AI badge (UXR-405), "View Evidence" button (AC-2)
- [ ] "View Evidence" opens `EvidenceBottomSheetComponent` listing correct citations for the selected suggestion (AC-2)
- [ ] When `lowConfidence: true` — `LowConfidenceBannerComponent` renders above cards with `aria-live="polite"` (AC-3)
- [ ] When `suggestions.length < 3` — `InsufficientEvidenceNoteComponent` renders below cards (Edge Case 1)
- [ ] HTTP 422 response triggers Empty state: "No suggestions available — manual coding required" with SCR-018 link (Edge Case 2)
- [ ] Loading state renders 3 skeleton cards; error state renders retry button; retry calls `facade.loadSuggestions(patientId)`
- [ ] `cdkTrapFocus` active in `EvidenceBottomSheetComponent`; accessible close returns focus to trigger button

---

## Implementation Checklist

- [ ] Define `IcdSuggestionDto`, `ClinicalFactCitationDto`, `CodingSuggestionResponseDto` DTOs
- [ ] Create `CodingSuggestionService` calling `GET /api/v1/patients/{id}/coding-suggestions`; map HTTP 422 to empty-state signal (Edge Case 2)
- [ ] Create `CodingSuggestionFacade` with Signal state: `loadingState`, `suggestions`, `lowConfidence`
- [ ] Create `SuggestionCardComponent` with code badge (monospace), confidence progress-bar, AI badge (UXR-405, AC-2), and `(viewEvidence)` output
- [ ] Create `EvidenceBottomSheetComponent` with `MAT_BOTTOM_SHEET_DATA` citations list and `cdkTrapFocus` (AC-2)
- [ ] Create `LowConfidenceBannerComponent` with `aria-live="polite"` amber banner (AC-3)
- [ ] Create `CodingSuggestionPanelComponent` with `@switch` state routing, `InsufficientEvidenceNoteComponent` for < 3 cards, SCR-018 link for empty (Edge Case 1, Edge Case 2)
- [ ] Register route under `ClinicalIntelligenceModule`; register facade and service in DI
