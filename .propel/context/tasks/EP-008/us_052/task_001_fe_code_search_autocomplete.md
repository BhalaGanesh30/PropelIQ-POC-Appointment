---
task_id: task_001
user_story: us_052
epic: EP-008
layer: Frontend
status: completed
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_052] Code Search with Autocomplete and Favorites
- **Story Location**: [.propel/context/tasks/EP-008/us_052/us_052.md](.propel/context/tasks/EP-008/us_052/us_052.md)
- **Acceptance Criteria**:
  - AC-1: Given I am in the coding workflow, When I type at least 2 characters in the code search field, Then an autocomplete dropdown appears with matching codes within 500 ms.
  - AC-2: Given search results are displayed, When I click on a code, Then the code is selected as my coding decision and added to the current encounter's coding record.
  - AC-3: Given I want to save a frequently used code, When I click the "Favorite" star icon on a code result, Then the code is added to my personal favorites list and appears at the top of future search results.
  - AC-4: Given I remove a code from favorites, When I click the "Unfavorite" icon, Then the code is removed from my favorites list and the change is persisted immediately.
- **Edge Cases**:
  - Edge Case 1: No search results — Empty state: "No codes found for your search term. Try a different keyword or code number." (below the autocomplete dropdown).
  - Edge Case 2: Deprecated/inactive codes — filtered from results by default; "Include inactive codes" toggle above results list allows advanced users to view them.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | .propel/context/docs/figma_spec.md#SCR-018 |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-028-code-search.[html\|png\|jpg]` — **Note: SCR-028 in us_052.md is incorrect. Correct screen is SCR-018 (Code Search). SCR-028 = Insurance Verification (EP-005).** |
| **Screen Spec** | SCR-018: Single-column. Search input at top. Autocomplete dropdown overlay. Results list below. Favorites section in sidebar (desktop) or collapsible section (mobile). |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-304, UXR-501, UXR-506 |
| **Design Tokens** | Star icon (filled = favorited, outline = unfavorited); selected code highlighted; autocomplete keyboard-navigable (UXR-506); touch targets ≥ 44×44px (UXR-304) |

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

Implement the SCR-018 Code Search screen as a standalone `CodeSearchComponent` that provides keyword/code-number autocomplete, favorites management, and "Include inactive codes" toggle. The component is routed to from the SCR-017 "Search Code" link (US_051/reject flow) and from SCR-017's Empty state manual coding link.

**Search behavior** (AC-1): A `mat-form-field` search input with debounce of 300ms (UXR-506) triggers the search after ≥ 2 characters. A `MatAutocomplete` overlay dropdown renders matching `CodeResultDto` items grouped by type (ICD-10 / CPT). Keyboard navigation through dropdown is required (UXR-506 — Arrow keys, Enter to select, Escape to close). Favorites are pinned at the top of the dropdown as a "Your Favorites" group.

**Code selection** (AC-2): Selecting a result emits a `(codeSelected)` output event with the `CodeResultDto`; if the component is used standalone it calls `CodingDecisionService.selectManualCode()` to record the decision; if embedded as a child, the parent handles the output.

**Favorites** (AC-3/AC-4): A `mat-icon-button` star icon on each result item toggles favorite state. Filled star = saved, outline star = not saved. Toggle calls `CodeSearchFacade.toggleFavorite(code)` which calls the respective add/remove API. Optimistic update applied — the Signal state updates immediately, reverted on API error with an inline error `MatSnackBar` toast.

**States**: Default (search input + favorites section), Loading (spinner in autocomplete dropdown), Empty (no results, Edge Case 1), Error ("Search unavailable" + retry), Validation (selected code highlighted in results).

---

## Dependent Tasks

- **us_052/task_002** — Provides `GET /api/v1/codes/search`, `GET|POST|DELETE /api/v1/users/me/code-favorites`.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `CodeSearchComponent` | CREATE | SCR-018 host; search input, MatAutocomplete overlay, results list, favorites section, Include inactive toggle |
| `CodeResultItemComponent` | CREATE | Single result row: code badge (monospace), description, type chip (ICD-10/CPT), star toggle (UXR-506) |
| `FavoritesSectionComponent` | CREATE | Sidebar (desktop) / collapsible mat-expansion-panel (mobile) listing user's favorited codes |
| `CodeSearchFacade` | CREATE | Signal state: `results`, `favorites`, `loadingState`, `selectedCode`, `includeDeprecated`; `toggleFavorite` with optimistic update |
| `CodeSearchService` | CREATE | `search(q, type, includeDeprecated): Observable<CodeSearchResponseDto>`; `getFavorites(): Observable<CodeFavoriteDto[]>`; `addFavorite(req)`, `removeFavorite(codeType, code)` |
| `CodeSearchResponseDto` | CREATE | `{ results: CodeResultDto[], totalCount: number }` |
| `CodeResultDto` | CREATE | `{ code: string, description: string, codeType: 'icd10'|'cpt', isDeprecated: boolean, isFavorited: boolean }` |
| `CodeFavoriteDto` | CREATE | `{ code: string, description: string, codeType: 'icd10'|'cpt' }` |
| SCR-018 routing | MODIFY | Register `CodeSearchComponent` route under `ClinicalIntelligenceModule`; navigable from SCR-017 links |

---

## Implementation Plan

1. **Define DTOs**: `CodeResultDto` (`code: string`, `description: string`, `codeType: 'icd10' | 'cpt'`, `isDeprecated: boolean`, `isFavorited: boolean`); `CodeSearchResponseDto` (`results: CodeResultDto[]`, `totalCount: number`); `CodeFavoriteDto` (`code`, `description`, `codeType`); `AddFavoriteRequestDto` (`code: string`, `codeType: 'icd10' | 'cpt'`).
2. **Create `CodeSearchService`**: `search(q: string, type: 'all'|'icd10'|'cpt', includeDeprecated: boolean): Observable<CodeSearchResponseDto>` — calls `GET /api/v1/codes/search?q={q}&type={type}&includeDeprecated={includeDeprecated}`; min 2 chars enforced at facade. `getFavorites(): Observable<CodeFavoriteDto[]>` → `GET /api/v1/users/me/code-favorites`. `addFavorite(req: AddFavoriteRequestDto)` → `POST /api/v1/users/me/code-favorites`. `removeFavorite(codeType, code)` → `DELETE /api/v1/users/me/code-favorites/{codeType}/{code}`.
3. **Create `CodeSearchFacade`**: `query = signal('')`; `results = signal<CodeResultDto[]>([])`; `favorites = signal<CodeFavoriteDto[]>([])`; `loadingState = signal<'idle'|'loading'|'loaded'|'empty'|'error'>('idle')`; `includeDeprecated = signal(false)`; `selectedCode = signal<CodeResultDto | null>(null)`. `loadResults()`: debounce 300ms, min 2 chars, call `CodeSearchService.search()`, set results or empty state. `toggleFavorite(item)`: optimistic update → `favorites.update(...)` → API call → revert on error + show snackbar.
4. **Create `CodeResultItemComponent`**: `@Input() result: CodeResultDto`; `@Input() isFavorited: boolean`; `@Output() favoriteToggled = new EventEmitter<CodeResultDto>()`. Renders: monospace code badge, description text, type chip ("ICD-10" or "CPT"), `mat-icon-button` star (filled `star` icon when favorited, `star_border` when not). Touch target ≥ 44×44px for star button (UXR-304). `aria-label` on star: "Add [code] to favorites" / "Remove [code] from favorites".
5. **Create `FavoritesSectionComponent`**: Sidebar on `≥ 960px` breakpoint (CSS `@media`); `mat-expansion-panel` on mobile (collapsible, UXR-304 touch targets). Renders a list of `CodeResultItemComponent` items from `facade.favorites()`. Clicking a favorite item emits `(favoriteCodeSelected)` output. "No favorites yet" empty state with instructional text.
6. **Create `CodeSearchComponent`**: `mat-form-field` with `matAutocomplete` directive connected to `[matAutocomplete]="auto"`. `MatAutocomplete` panel groups: "Your Favorites" group (rendered when query ≥ 2 chars and favorites match), "Results" group. `@if(query.length >= 2)` else show `FavoritesSectionComponent` full panel. "Include inactive codes" `mat-slide-toggle` above results (Edge Case 2) bound to `facade.includeDeprecated` signal; toggle triggers `facade.loadResults()`. Empty state (Edge Case 1) rendered inside autocomplete panel via `mat-option` with non-selectable text. Keyboard navigation handled natively by `MatAutocomplete`; `(optionSelected)` event emits selected `CodeResultDto` to `facade.selectedCode` and triggers `(codeSelected)` output.
7. **Handle code selection** (AC-2): On `(optionSelected)`, set `facade.selectedCode(item)`. If `@Input() standalone = true` (routed view), call `CodingDecisionService.selectManualCode(item)` to record manual coding decision. If `@Input() standalone = false` (embedded), emit `(codeSelected)` output for parent to handle.
8. **Register route and module**: Route `coding/search` in `ClinicalIntelligenceModule` pointing to `CodeSearchComponent`. Load favorites on component init. Register `CodeSearchFacade` and `CodeSearchService` in providers.

---

## Current Project State

```
src/
├── app/
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   ├── coding-suggestion-panel/              ← EXISTS (US_049-051)
│   │   │   │   └── code-search/
│   │   │   │       ├── code-search.component.ts          ← CREATE
│   │   │   │       ├── code-result-item/
│   │   │   │       │   └── code-result-item.component.ts ← CREATE
│   │   │   │       └── favorites-section/
│   │   │   │           └── favorites-section.component.ts ← CREATE
│   │   │   ├── facades/
│   │   │   │   └── code-search.facade.ts                 ← CREATE
│   │   │   ├── services/
│   │   │   │   └── code-search.service.ts                ← CREATE
│   │   │   └── models/
│   │   │       └── code-search.dto.ts                    ← CREATE
│   └── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `modules/clinical-intelligence/components/code-search/code-search.component.ts` | SCR-018 host: search input, MatAutocomplete, favorites, Include inactive toggle (Edge Case 2) |
| CREATE | `modules/clinical-intelligence/components/code-search/code-result-item/code-result-item.component.ts` | Result row: monospace badge, description, type chip, star toggle (UXR-304, UXR-506) |
| CREATE | `modules/clinical-intelligence/components/code-search/favorites-section/favorites-section.component.ts` | Sidebar/collapsible favorites list; empty state |
| CREATE | `modules/clinical-intelligence/facades/code-search.facade.ts` | Signal state: results, favorites, loadingState, includeDeprecated; optimistic favorite toggle |
| CREATE | `modules/clinical-intelligence/services/code-search.service.ts` | GET search, GET/POST/DELETE favorites endpoints |
| CREATE | `modules/clinical-intelligence/models/code-search.dto.ts` | CodeResultDto, CodeSearchResponseDto, CodeFavoriteDto, AddFavoriteRequestDto |
| MODIFY | `modules/clinical-intelligence/clinical-intelligence.module.ts` | Register CodeSearchComponent route (`coding/search`), CodeSearchFacade, CodeSearchService |

---

## External References

- Angular Material Autocomplete: https://material.angular.io/components/autocomplete
- Angular Material Slide Toggle: https://material.angular.io/components/slide-toggle
- Angular Signals debounce: https://angular.dev/guide/signals
- Angular CDK BreakpointObserver: https://material.angular.io/cdk/layout/overview
- UXR-506: Autocomplete results within 300ms; keyboard-navigable dropdown (FR-MC-004)
- UXR-304: Touch targets ≥ 44×44px on mobile
- NFR-002: API search response ≤ 500ms p95
- FR-MC-004 [DETERMINISTIC]: Code search with autocomplete and favorites

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

- [ ] Typing ≥ 2 characters triggers autocomplete with results within 500ms; debounce is 300ms (AC-1, UXR-506)
- [ ] Autocomplete dropdown is keyboard-navigable (Arrow keys, Enter to select, Escape to close) (UXR-506)
- [ ] Selecting a result records coding decision and emits `(codeSelected)` event (AC-2)
- [ ] Star icon click adds code to favorites; favorites appear at top of subsequent results; star turns filled (AC-3)
- [ ] Unfavorite click removes code from favorites immediately (optimistic); error triggers snackbar + revert (AC-4)
- [ ] Empty state "No codes found..." rendered when search returns zero results (Edge Case 1)
- [ ] "Include inactive codes" toggle re-triggers search with `includeDeprecated=true`; deprecated codes appear in results (Edge Case 2)
- [ ] Favorites section renders as sidebar on desktop, collapsible panel on mobile (UXR-304 touch targets ≥ 44×44px)
- [ ] `FavoritesSectionComponent` shows "No favorites yet" empty state when favorites list is empty

---

## Implementation Checklist

- [ ] Define `CodeResultDto`, `CodeSearchResponseDto`, `CodeFavoriteDto`, `AddFavoriteRequestDto` DTOs
- [ ] Create `CodeSearchService`: GET search (q, type, includeDeprecated); GET/POST/DELETE favorites endpoints
- [ ] Create `CodeSearchFacade`: Signal state with 300ms debounce; optimistic favorite toggle with error revert (AC-3, AC-4)
- [ ] Create `CodeResultItemComponent`: monospace code badge, description, type chip, star icon button ≥ 44×44px (UXR-304, UXR-506)
- [ ] Create `FavoritesSectionComponent`: sidebar (desktop) / mat-expansion-panel (mobile); "No favorites yet" empty state
- [ ] Create `CodeSearchComponent`: MatAutocomplete keyboard-navigable (UXR-506); "Include inactive codes" toggle (Edge Case 2); Empty state inside panel (Edge Case 1); `(codeSelected)` output (AC-2)
- [ ] Register route `coding/search` and providers in `ClinicalIntelligenceModule`
