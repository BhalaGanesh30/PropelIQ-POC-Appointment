---
task_id: task_001
user_story: us_048
epic: EP-007
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_048] Chronological Clinical Timeline View
- **Story Location**: [.propel/context/tasks/EP-007/us_048/us_048.md](.propel/context/tasks/EP-007/us_048/us_048.md)
- **Acceptance Criteria**:
  - AC-1: Given I open the clinical timeline, When the view renders, Then all clinical events (medications started/stopped, diagnoses, allergies recorded, documents uploaded) are displayed in reverse chronological order.
  - AC-2: Given the timeline is displayed, When I apply a category filter, Then only matching entries are shown within 500 ms.
  - AC-3: Given the timeline is displayed, When I apply a date range filter, Then only events within the specified range are shown.
  - AC-4: Given I click "Print Timeline," When the action is invoked, Then a print-optimized rendering is produced with patient name, date range, and event list formatted for paper output.
- **Edge Cases**:
  - Edge Case 1: No timeline events — empty state: "No clinical events recorded yet."
  - Edge Case 2: Very long timelines spanning multiple years — year-based grouping with collapse/expand; server-side filtering applied via API query parameters.

---

## Design References (UI Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | `.propel/context/docs/figma_spec.md#SCR-015` |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe — upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-015-clinical-timeline.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | SCR-015 (Clinical Timeline) |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-303 |
| **Design Tokens** | Refer to global design tokens; timeline category colors consistent with severity/status palette |

> **Note — Screen ID Correction**: US_048 references SCR-026 (Daily Schedule View) in the story file. The correct screen for the clinical timeline is **SCR-015**. SCR-026 belongs to the Staff Daily Schedule (EP-004). All implementation targets SCR-015.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Component Library | Angular Material + CDK | 17.x |
| State/Reactivity | Angular Signals + RxJS | 7.x |
| Forms | Angular Reactive Forms (date range) | 17.x |
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

Implement the `ClinicalTimelineComponent` (SCR-015) as the Timeline tab within the 360° Patient Profile (SCR-014). The component loads timeline events from `GET /api/v1/patients/{id}/timeline` on tab activation (lazy load, consistent with US_045 pattern). Events are rendered as a single-column vertical timeline with left date markers and right event cards (SCR-015 Layout). Category filter chips (Medications, Diagnoses, Allergies, Documents) and a date-range picker are pinned in a sticky filter bar above the list; applying either filter triggers a new API call with the selected parameters within 500 ms (AC-2, AC-3). For timelines spanning multiple years (Edge Case 2), events are grouped by year using Angular CDK `mat-expansion-panel` with collapse/expand per year group. Empty state shows "No clinical events recorded yet." with a document upload CTA (Edge Case 1, SCR-015 Empty state). Loading state shows skeleton timeline cards; error state shows a retry banner (SCR-015 states). "Print Timeline" button invokes `window.print()` against a print-optimized stylesheet rendering the filtered event list with patient name, date range header, and event cards (AC-4, SCR-015 Validation state). Mobile layout at 375px and 768px: timeline remains single-column; filter chips scroll horizontally (UXR-301, UXR-303). Full WCAG 2.1 AA: keyboard navigation, visible focus rings, 4.5:1 contrast on category chips and event card text (UXR-201, UXR-202).

---

## Dependent Tasks

- **us_048/task_002** — `GET /api/v1/patients/{id}/timeline` API with server-side filtering and year grouping must be implemented.
- **us_045/task_001** — Patient profile tab shell (SCR-014) must exist to host the Timeline tab stub.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ClinicalTimelineComponent` | CREATE | SCR-015: vertical timeline, filter bar, year groups, print button |
| `TimelineEventCardComponent` | CREATE | Single event card: event type icon, category chip, description, date label |
| `TimelineYearGroupComponent` | CREATE | `mat-expansion-panel` grouping events by year; default expanded for current year |
| `TimelineFilterBarComponent` | CREATE | Sticky bar: category filter chips (`MatChipListbox`) + `MatDateRangePicker` |
| `TimelineEmptyStateComponent` | CREATE | "No clinical events recorded yet." + upload document CTA (Edge Case 1) |
| `TimelineSkeletonComponent` | CREATE | Skeleton loader matching timeline card layout (SCR-015 Loading state) |
| `ClinicalTimelineService` | CREATE | Angular service: `getTimeline(patientId, params)` — `HttpClient.get` with category and date range query params |
| `ClinicalTimelineFacade` | CREATE | Signal state: `events`, `loading`, `error`, `activeFilters`; `applyFilters()` triggers new API call |
| `ProfileTimelineTabComponent` | MODIFY | Replace stub (from US_045) with `ClinicalTimelineComponent`; pass `patientId` input |

---

## Implementation Plan

1. **Create `ClinicalTimelineService`**: `getTimeline(patientId: string, params: TimelineQueryParams): Observable<TimelineResponseDto>`. `TimelineQueryParams`: `{ category?: string, dateFrom?: string, dateTo?: string }`. Uses `HttpParams` to build query string. Returns `{ events: TimelineEventDto[], totalCount: number }` where `TimelineEventDto`: `{ eventId, eventType, category, description, eventDate, patientId }`.
2. **Create `ClinicalTimelineFacade`**: Signal state: `events = signal<TimelineEventDto[]>([])`, `loading = signal(false)`, `error = signal<string | null>(null)`, `activeFilters = signal<TimelineQueryParams>({})`. `load(patientId, params)`: set loading, call service, update events. `applyFilters(params)`: merge with current filters, call `load()`. Computed: `groupedByYear = computed(() => groupEventsByYear(events()))`.
3. **Create `TimelineFilterBarComponent`**: Sticky `position: sticky; top: 0` bar. `MatChipListbox` with chips: All, Medications, Diagnoses, Allergies, Documents — single selection. `MatDateRangePicker` with `[start]` and `[end]` `FormControl`. On chip or date change, emit `(filterChange): EventEmitter<TimelineQueryParams>` to parent facade within a `debounceTime(150)` pipe to avoid double-triggering (AC-2, AC-3). Active filter chip highlighted with filled style; date range displays formatted "Jan 1 – Dec 31, 2025" label.
4. **Create `TimelineEventCardComponent`**: Inputs: `[event]: TimelineEventDto`. Display: category icon (medication: pill, diagnosis: stethoscope, allergy: warning, document: file) using `<mat-icon>`, category chip with category-specific color class, description text, formatted event date (`'MMM d, yyyy'`). Use `<time [dateTime]="event.eventDate">` for semantic date (UXR-201).
5. **Create `TimelineYearGroupComponent`**: `<mat-expansion-panel>` with header showing year label and event count badge. Default: expanded for current year, collapsed for all prior years. Inputs: `[year]: number`, `[events]: TimelineEventDto[]`. Renders `@for (event of events; track event.eventId)` with `TimelineEventCardComponent`. Apply CSS `::before` pseudo-element vertical line on the timeline axis connecting cards within each group (Edge Case 2).
6. **Create `ClinicalTimelineComponent`**: On init, inject `patientId` from route params. Call `ClinicalTimelineFacade.load(patientId, {})`. Show `TimelineSkeletonComponent` while `loading()` is true. Show `TimelineEmptyStateComponent` when `events().length === 0` and not loading (Edge Case 1). Render `@for (group of groupedByYear(); track group.year)` using `TimelineYearGroupComponent`. Bind `TimelineFilterBarComponent (filterChange)` to `ClinicalTimelineFacade.applyFilters()`.
7. **Implement print functionality (AC-4)**: "Print Timeline" `<button mat-button>` with print icon calls `window.print()`. Apply `@media print` CSS rules in `clinical-timeline.component.scss`: hide filter bar, navigation, and other page elements; show a print header with `Patient Name`, `MRN`, and applied date range; render full event list without collapse (expand all year groups via `expanded = true` before printing using `AfterViewInit` + `ViewChildren(MatExpansionPanel)`). Restore expansion state after print via `window.addEventListener('afterprint', ...)`.
8. **Accessibility and responsive**: Filter chips have `aria-label="Filter by {{ chip.label }}"`. Date range inputs have `aria-label` and `aria-describedby` for error text. Event cards use semantic `<article>` element with `aria-label="{{ event.category }} event on {{ event.eventDate }}"` (UXR-201). All interactive elements keyboard-navigable (UXR-202). At 375px: filter chips scroll horizontally with `overflow-x: auto`; timeline cards remain full-width (UXR-301). Below 768px: filter chips switch to horizontal scroll strip (UXR-303).

---

## Current Project State

```
src/
├── app/
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   ├── patient-profile/
│   │   │   │   │   ├── patient-profile.component.ts        ← EXISTS (US_045/046)
│   │   │   │   │   └── tabs/
│   │   │   │   │       ├── profile-summary-tab.component.ts    ← EXISTS (US_045)
│   │   │   │   │       ├── profile-conflicts-tab.component.ts  ← EXISTS (US_046)
│   │   │   │   │       └── profile-timeline-tab.component.ts   ← MODIFY (replace stub with ClinicalTimelineComponent)
│   │   │   │   └── clinical-timeline/                          ← CREATE
│   │   │   │       ├── clinical-timeline.component.ts          ← CREATE
│   │   │   │       ├── clinical-timeline.component.html        ← CREATE
│   │   │   │       ├── clinical-timeline.component.scss        ← CREATE
│   │   │   │       ├── timeline-event-card.component.ts        ← CREATE
│   │   │   │       ├── timeline-year-group.component.ts        ← CREATE
│   │   │   │       ├── timeline-filter-bar.component.ts        ← CREATE
│   │   │   │       ├── timeline-empty-state.component.ts       ← CREATE
│   │   │   │       └── timeline-skeleton.component.ts          ← CREATE
│   │   │   ├── services/
│   │   │   │   └── clinical-timeline.service.ts                ← CREATE
│   │   │   └── facades/
│   │   │       └── clinical-timeline.facade.ts                 ← CREATE
│   │   └── [existing modules...]
│   └── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/modules/clinical-intelligence/components/clinical-timeline/clinical-timeline.component.ts` | Main timeline: load on activate, year-grouped events, filter bar, print button |
| CREATE | `app/modules/clinical-intelligence/components/clinical-timeline/clinical-timeline.component.html` | Template: filter bar, skeleton, empty state, year group list, error state |
| CREATE | `app/modules/clinical-intelligence/components/clinical-timeline/clinical-timeline.component.scss` | Timeline axis line, card layout, category chip colors, @media print rules |
| CREATE | `app/modules/clinical-intelligence/components/clinical-timeline/timeline-event-card.component.ts` | Event card: category icon, chip, description, semantic date |
| CREATE | `app/modules/clinical-intelligence/components/clinical-timeline/timeline-year-group.component.ts` | mat-expansion-panel year grouping with event count badge |
| CREATE | `app/modules/clinical-intelligence/components/clinical-timeline/timeline-filter-bar.component.ts` | Sticky filter bar: category chips + date range picker; emits filterChange |
| CREATE | `app/modules/clinical-intelligence/components/clinical-timeline/timeline-empty-state.component.ts` | "No clinical events recorded yet." with upload CTA |
| CREATE | `app/modules/clinical-intelligence/components/clinical-timeline/timeline-skeleton.component.ts` | Skeleton loader for timeline cards |
| CREATE | `app/modules/clinical-intelligence/services/clinical-timeline.service.ts` | HttpClient service: getTimeline with category + date query params |
| CREATE | `app/modules/clinical-intelligence/facades/clinical-timeline.facade.ts` | Signal state: events, loading, error, activeFilters, groupedByYear computed |
| MODIFY | `app/modules/clinical-intelligence/components/patient-profile/tabs/profile-timeline-tab.component.ts` | Replace stub with ClinicalTimelineComponent, pass patientId |

---

## External References

- Angular Material Datepicker: https://material.angular.io/components/datepicker/overview
- Angular Material Expansion Panel: https://material.angular.io/components/expansion/overview
- Angular Material Chips: https://material.angular.io/components/chips/overview
- CSS @media print: https://developer.mozilla.org/en-US/docs/Web/CSS/@media#print
- WCAG 2.1 AA: https://www.w3.org/TR/WCAG21/
- FR-CA-005: Chronological timeline view with filter and print support
- UXR-201: WCAG 2.1 AA color contrast (4.5:1)
- UXR-202: Full keyboard navigation with visible focus indicators
- UXR-301: Support mobile (375px), tablet (768px), desktop (1440px) breakpoints
- UXR-303: Data tables switch to card-based layout below 768px
- SCR-015 spec: `.propel/context/docs/figma_spec.md#SCR-015`

---

## Build Commands

```bash
# Install dependencies
npm install

# Serve app locally
ng serve

# Run unit tests
ng test --include="**/clinical-timeline/**"

# Build production
ng build --configuration=production
```

---

## Implementation Validation Strategy

- [ ] Timeline tab renders events in reverse chronological order on load (AC-1)
- [ ] Category filter chip applied: only matching events shown; API call made with `?category=` param within 500 ms (AC-2)
- [ ] Date range filter applied: only events within range shown; API call made with `?dateFrom=&dateTo=` params (AC-3)
- [ ] "Print Timeline" button triggers `window.print()`; print stylesheet hides filter bar and shows patient header with name, MRN, and date range (AC-4)
- [ ] Empty state "No clinical events recorded yet." shown when `events().length === 0` (Edge Case 1)
- [ ] Year-based grouping applied; current year expanded by default; older years collapsed (Edge Case 2)
- [ ] All year groups expand before print; state restored after `afterprint` event
- [ ] Loading state: skeleton timeline cards visible during fetch (SCR-015 Loading state)
- [ ] Error state: retry banner shown on API failure (SCR-015 Error state)
- [ ] Mobile 375px: filter chips scroll horizontally; timeline cards full-width (UXR-301)
- [ ] Category chips and event card text pass 4.5:1 contrast ratio (UXR-201)
- [ ] All interactive elements keyboard-navigable with visible focus rings (UXR-202)

---

## Implementation Checklist

- [ ] Create `ClinicalTimelineService` with `getTimeline(patientId, params)` using `HttpParams` for category + date filters
- [ ] Create `ClinicalTimelineFacade` with Signal state: events, loading, error, activeFilters, `groupedByYear` computed
- [ ] Create `TimelineFilterBarComponent`: sticky, category `MatChipListbox`, `MatDateRangePicker`, debounced `(filterChange)` output (AC-2, AC-3)
- [ ] Create `TimelineYearGroupComponent`: `mat-expansion-panel`, current year expanded by default, event count badge (Edge Case 2)
- [ ] Create `TimelineEventCardComponent`: category icon, chip, description, semantic `<time>` element
- [ ] Create `ClinicalTimelineComponent`: load on activate, render year groups, wire filter bar, empty/loading/error states (AC-1, Edge Case 1)
- [ ] Implement `@media print` stylesheet: show patient header, expand all year groups before print, restore state after `afterprint` (AC-4)
- [ ] Apply WCAG 2.1 AA: aria-labels on chips and cards, keyboard navigation, 4.5:1 contrast, focus-visible rings (UXR-201/202)
