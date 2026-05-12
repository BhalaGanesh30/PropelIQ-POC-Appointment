---
task_id: task_001
user_story: us_050
epic: EP-008
layer: Frontend
status: completed
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_050] CPT and E/M Mapping Suggestions
- **Story Location**: [.propel/context/tasks/EP-008/us_050/us_050.md](.propel/context/tasks/EP-008/us_050/us_050.md)
- **Acceptance Criteria**:
  - AC-1: Given a patient's clinical profile and appointment details are available, When I request CPT/E/M suggestions, Then the system returns ranked CPT codes and an E/M level suggestion with confidence scores and rationale within 2.5 seconds p95.
  - AC-2: Given CPT suggestions are displayed, When I view a suggestion card, Then the CPT code, description, confidence score, rationale text, and a link to supporting clinical evidence are all visible.
  - AC-3: Given an E/M level suggestion is provided, When I view the E/M mapping, Then the suggested E/M level is explained with the contributing clinical complexity factors.
  - AC-4: Given the AI model confidence for CPT is below threshold, When suggestions are returned, Then a "Manual coding recommended" indicator is shown with the low-confidence flag.
- **Edge Cases**:
  - Edge Case 1: Appointment type not mappable to CPT — render CPT section Empty state: "No CPT suggestion available for this appointment type" with a link to the code search workflow (SCR-018).
  - Edge Case 2: Stale CPT database (> 90 days) — render amber `StaleCptDatabaseBannerComponent` above CPT section when API returns `staleDatabaseWarning: true`.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | .propel/context/docs/figma_spec.md#SCR-017 |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-027-coding-suggestions.[html\|png\|jpg]` — **Note: SCR-027 in us_050.md is incorrect. Correct screen is SCR-017 (Coding Suggestion Review). SCR-027 = Staff-Assisted Booking (EP-004 scheduling flow).** |
| **Screen Spec** | SCR-017: Two-section layout. ICD-10 section (top, US_049). CPT section (below): CPT suggestion cards stacked vertically + E/M level card. Summary bar at top. |
| **UXR Requirements** | UXR-108, UXR-201, UXR-202, UXR-301, UXR-405, UXR-111 |
| **Design Tokens** | AI marker badge (UXR-405), confidence bar (UXR-505), monospace JetBrains Mono for code display, destructive reject button (UXR-111), rich tooltip (UXR-204) for complexity factors |

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

Extend `CodingSuggestionPanelComponent` (US_049) to render the CPT section below the existing ICD-10 section, completing SCR-017's two-section layout. The CPT section contains:

1. **CPT suggestion cards** (`CptSuggestionCardComponent`): code badge (monospace JetBrains Mono), description, confidence progress bar, rationale, AI badge (UXR-405), "View Evidence" button reusing `EvidenceBottomSheetComponent` from US_049.
2. **E/M level card** (`EmLevelCardComponent`): distinct card for the E/M level suggestion (e.g., "99213"); shows E/M level code, description, confidence bar, and a collapsible list of contributing clinical complexity factors (AC-3); complexity factors use a rich `MatTooltip` (UXR-204) on hover for factor definitions.
3. **Stale database banner** (`StaleCptDatabaseBannerComponent`): amber `mat-card` rendered above the CPT section when `cptResponse.staleDatabaseWarning === true` (Edge Case 2); text "CPT code database may be outdated — suggestions may include deprecated codes. Contact your administrator."
4. **Low-confidence banner**: reuse `LowConfidenceBannerComponent` from US_049 but scoped to the CPT section when `cptResponse.lowConfidence === true` (AC-4); text "Manual coding recommended — CPT confidence is below the minimum threshold."

CPT section states: Loading (skeleton cards, shared with panel loading state), Empty — no appointment type match (Edge Case 1 — "No CPT suggestion available for this appointment type" + SCR-018 link), Error (retry button), Default (cards rendered). The CPT section state is managed independently from the ICD-10 section via `CptSuggestionFacade` to allow independent loading and retry.

---

## Dependent Tasks

- **us_050/task_002** — Provides `GET /api/v1/patients/{id}/coding-suggestions/cpt` endpoint.
- **us_049/task_001** — `CodingSuggestionPanelComponent` and `EvidenceBottomSheetComponent` must exist before this task extends them.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `CodingSuggestionPanelComponent` | MODIFY | Add CPT section below ICD-10 section; inject `CptSuggestionFacade`; add CPT section state switch |
| `CptSuggestionCardComponent` | CREATE | CPT card: code badge (monospace), description, confidence bar, AI badge (UXR-405), View Evidence button |
| `EmLevelCardComponent` | CREATE | Distinct E/M level card: code, description, confidence bar, collapsible complexity factors list (AC-3), UXR-204 tooltip per factor |
| `StaleCptDatabaseBannerComponent` | CREATE | Amber banner with warning icon; shown when `staleDatabaseWarning: true` (Edge Case 2) |
| `CptSuggestionFacade` | CREATE | Signal state: `cptSuggestions`, `emSuggestion`, `cptLoadingState`, `cptLowConfidence`, `staleDatabaseWarning` |
| `CptSuggestionService` | CREATE | HTTP service: `GET /api/v1/patients/{id}/coding-suggestions/cpt?appointmentId=`; maps `CptSuggestionResponseDto` |
| `CptSuggestionResponseDto` | CREATE | `{ cptSuggestions: CptSuggestionDto[], emSuggestion: EmSuggestionDto, lowConfidence: boolean, staleDatabaseWarning: boolean, noSuggestionForAppointmentType: boolean }` |
| `CptSuggestionDto` | CREATE | `{ decisionId, cptCode, description, confidence, rationale, citations: ClinicalFactCitationDto[] }` |
| `EmSuggestionDto` | CREATE | `{ decisionId, emLevel, description, confidence, rationale, complexityFactors: string[] }` |

---

## Implementation Plan

1. **Define DTOs**: `CptSuggestionDto` (`decisionId: string`, `cptCode: string`, `description: string`, `confidence: number`, `rationale: string`, `citations: ClinicalFactCitationDto[]`); `EmSuggestionDto` (`decisionId: string`, `emLevel: string`, `description: string`, `confidence: number`, `rationale: string`, `complexityFactors: string[]`); `CptSuggestionResponseDto` (`cptSuggestions`, `emSuggestion`, `lowConfidence: boolean`, `staleDatabaseWarning: boolean`, `noSuggestionForAppointmentType: boolean`). Reuse `ClinicalFactCitationDto` from US_049.
2. **Create `CptSuggestionService`**: `getCptSuggestions(patientId: string, appointmentId: string): Observable<CptSuggestionResponseDto>` — calls `GET /api/v1/patients/{patientId}/coding-suggestions/cpt?appointmentId={appointmentId}`. Map `noSuggestionForAppointmentType: true` response to Empty signal (Edge Case 1) via `map` operator.
3. **Create `CptSuggestionFacade`**: Signals: `cptLoadingState = signal<'idle'|'loading'|'loaded'|'empty'|'error'>('idle')`, `cptSuggestions = signal<CptSuggestionDto[]>([])`, `emSuggestion = signal<EmSuggestionDto | null>(null)`, `cptLowConfidence = signal(false)`, `staleDatabaseWarning = signal(false)`. `loadCptSuggestions(patientId, appointmentId)` manages state transitions.
4. **Create `CptSuggestionCardComponent`**: `@Input() suggestion: CptSuggestionDto`. Code badge: `<span class="code-badge font-mono">{{ suggestion.cptCode }}</span>`. `mat-progress-bar` for confidence. AI badge (UXR-405). "View Evidence" button emits `(viewEvidence)` output; opens `EvidenceBottomSheetComponent` from US_049.
5. **Create `EmLevelCardComponent`**: `@Input() emSuggestion: EmSuggestionDto`. E/M level code badge (monospace, distinct purple tint to differentiate from CPT). Confidence bar. Collapsible `mat-expansion-panel` for complexity factors — `@for(factor of emSuggestion.complexityFactors)` each with a `matTooltip` showing the factor definition (UXR-204, AC-3).
6. **Create `StaleCptDatabaseBannerComponent`**: Amber `mat-card` with warning icon. Text: "CPT code database may be outdated — suggestions may include deprecated codes. Contact your administrator." Rendered via `@if(facade.staleDatabaseWarning())` (Edge Case 2).
7. **Create CPT section Empty state**: Within `CodingSuggestionPanelComponent` CPT section: `@if(cptFacade.cptLoadingState() === 'empty')` → display "No CPT suggestion available for this appointment type" with `routerLink` to SCR-018 code search (Edge Case 1).
8. **Modify `CodingSuggestionPanelComponent`**: Inject `CptSuggestionFacade`. Add CPT section below ICD-10 section using `@switch` on `cptFacade.cptLoadingState()`. Render `StaleCptDatabaseBannerComponent`, `LowConfidenceBannerComponent` (scoped to CPT, AC-4), `@for` `CptSuggestionCardComponent`, and `EmLevelCardComponent`. Trigger `cptFacade.loadCptSuggestions(patientId, appointmentId)` on panel init alongside ICD-10 load.

---

## Current Project State

```
src/
├── app/
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   └── coding-suggestion-panel/
│   │   │   │       ├── coding-suggestion-panel.component.ts   ← MODIFY (add CPT section)
│   │   │   │       ├── suggestion-card/                       ← EXISTS (US_049, ICD-10)
│   │   │   │       ├── evidence-bottom-sheet/                 ← EXISTS (US_049, reused)
│   │   │   │       ├── low-confidence-banner/                 ← EXISTS (US_049, reused)
│   │   │   │       ├── cpt-suggestion-card/
│   │   │   │       │   └── cpt-suggestion-card.component.ts   ← CREATE
│   │   │   │       ├── em-level-card/
│   │   │   │       │   └── em-level-card.component.ts         ← CREATE
│   │   │   │       └── stale-cpt-database-banner/
│   │   │   │           └── stale-cpt-database-banner.component.ts ← CREATE
│   │   │   ├── facades/
│   │   │   │   ├── coding-suggestion.facade.ts                ← EXISTS (US_049)
│   │   │   │   └── cpt-suggestion.facade.ts                   ← CREATE
│   │   │   ├── services/
│   │   │   │   ├── coding-suggestion.service.ts               ← EXISTS (US_049)
│   │   │   │   └── cpt-suggestion.service.ts                  ← CREATE
│   │   │   └── models/
│   │   │       ├── coding-suggestion.dto.ts                   ← EXISTS (US_049)
│   │   │       └── cpt-suggestion.dto.ts                      ← CREATE
│   └── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `modules/clinical-intelligence/components/coding-suggestion-panel/coding-suggestion-panel.component.ts` | Add CPT section below ICD-10; inject CptSuggestionFacade; trigger cpt load on init |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/cpt-suggestion-card/cpt-suggestion-card.component.ts` | CPT card: monospace badge, confidence bar, AI badge (UXR-405), View Evidence button |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/em-level-card/em-level-card.component.ts` | E/M level card: code, description, confidence bar, collapsible complexity factors (AC-3), UXR-204 tooltip |
| CREATE | `modules/clinical-intelligence/components/coding-suggestion-panel/stale-cpt-database-banner/stale-cpt-database-banner.component.ts` | Amber banner for stale CPT DB (Edge Case 2) |
| CREATE | `modules/clinical-intelligence/facades/cpt-suggestion.facade.ts` | Signal state: cptSuggestions, emSuggestion, cptLoadingState, cptLowConfidence, staleDatabaseWarning |
| CREATE | `modules/clinical-intelligence/services/cpt-suggestion.service.ts` | GET /api/v1/patients/{id}/coding-suggestions/cpt; maps noSuggestionForAppointmentType |
| CREATE | `modules/clinical-intelligence/models/cpt-suggestion.dto.ts` | CptSuggestionDto, EmSuggestionDto, CptSuggestionResponseDto |

---

## External References

- Angular Signals: https://angular.dev/guide/signals
- Angular Material Progress Bar: https://material.angular.io/components/progress-bar
- Angular Material Expansion Panel: https://material.angular.io/components/expansion
- Angular Material Tooltip: https://material.angular.io/components/tooltip
- UXR-108: Coding suggestion cards with code badge, confidence, rationale, action buttons
- UXR-204: Rich tooltip with code descriptions (complexity factor definitions on `EmLevelCardComponent`)
- UXR-405: AI-generated content badge / background tint
- AIR-005: Fallback to manual coding when confidence below threshold (AC-4)
- FR-MC-002 [HYBRID]: CPT and E/M mapping suggestions with explainable rationale
- SCR-017: Two-section layout — CPT section below ICD-10 section

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

- [X] CPT section renders below ICD-10 section in `CodingSuggestionPanelComponent` with Loading/Empty/Error/Default states independent of ICD-10 section
- [X] Each `CptSuggestionCardComponent` shows: CPT code (monospace badge), description, confidence bar, AI badge (UXR-405), "View Evidence" button opening `EvidenceBottomSheetComponent` (AC-2)
- [X] `EmLevelCardComponent` renders E/M level code, description, confidence bar, and collapsible complexity factors; UXR-204 tooltip shown on each factor (AC-3)
- [X] When `cptResponse.lowConfidence: true` — `LowConfidenceBannerComponent` rendered above CPT cards with "Manual coding recommended" text (AC-4)
- [X] When `staleDatabaseWarning: true` — `StaleCptDatabaseBannerComponent` renders above CPT section (Edge Case 2)
- [X] When `noSuggestionForAppointmentType: true` — CPT section Empty state renders with SCR-018 navigation link (Edge Case 1)
- [X] CPT facade signals update independently; retry in CPT error state calls `cptFacade.loadCptSuggestions()` only

---

## Implementation Checklist

- [X] Define `CptSuggestionDto`, `EmSuggestionDto`, `CptSuggestionResponseDto` DTOs; reuse `ClinicalFactCitationDto` from US_049
- [X] Create `CptSuggestionService` calling `GET /api/v1/patients/{id}/coding-suggestions/cpt?appointmentId=`; map `noSuggestionForAppointmentType` to empty signal (Edge Case 1)
- [X] Create `CptSuggestionFacade` with independent Signal state for CPT section
- [X] Create `CptSuggestionCardComponent` with monospace code badge, confidence bar, AI badge (UXR-405), View Evidence output (AC-2)
- [X] Create `EmLevelCardComponent` with collapsible complexity factors and UXR-204 tooltips per factor (AC-3)
- [X] Create `StaleCptDatabaseBannerComponent` for stale DB warning (Edge Case 2)
- [X] Modify `CodingSuggestionPanelComponent` to add CPT section with `@switch` state routing; `LowConfidenceBannerComponent` scoped to CPT section (AC-4); CPT Empty state with SCR-018 link (Edge Case 1)
- [X] Register `CptSuggestionFacade` and `CptSuggestionService` in `ClinicalIntelligenceModule` providers
