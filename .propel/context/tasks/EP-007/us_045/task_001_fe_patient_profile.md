---
task_id: task_001
user_story: us_045
epic: EP-007
layer: Frontend
status: not-started
effort_hours: 8
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_045] 360-Degree Patient Profile View
- **Story Location**: [.propel/context/tasks/EP-007/us_045/us_045.md](.propel/context/tasks/EP-007/us_045/us_045.md)
- **Acceptance Criteria**:
  - AC-1: Given I open a patient profile, When the page loads, Then a consolidated view of medications, allergies, diagnoses, and timeline entries is rendered within 3 seconds p95.
  - AC-2: Given the 360-degree profile is displayed, When I hover over or click on an extracted data point, Then a source traceability link to the originating document is shown.
  - AC-3: Given the profile contains data from multiple documents, When I view a medication entry, Then the source document name, upload date, and extraction confidence are visible on demand.
  - AC-4: Given a profile has no clinical data yet, When the profile loads, Then an empty state message is displayed: "No clinical data extracted. Upload documents to populate this profile."
- **Edge Cases**:
  - Edge Case 1: One data source fails to load — profile renders with available data; a warning banner is shown indicating partial data and naming the unavailable source.
  - Edge Case 2: Very large profiles (100+ extracted facts) — virtual scrolling and section-based lazy loading applied; each category section loads independently.

---

## Design References (UI Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | `.propel/context/docs/figma_spec.md#SCR-014` |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe — upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-014-patient-profile-360.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | SCR-014 (360° Patient Profile) |
| **UXR Requirements** | UXR-107, UXR-201, UXR-202, UXR-204, UXR-301, UXR-303, UXR-405 |
| **Design Tokens** | Refer to global design tokens (typography, color, spacing) |

> **Note — Screen ID Correction**: US_045 references SCR-025 (Queue Dashboard) in the story file. The correct screen for the 360-degree patient profile is **SCR-014**. SCR-025 belongs to the Staff Queue Dashboard (EP-004). All implementation targets SCR-014.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Component Library | Angular Material + CDK | 17.x |
| State/Reactivity | Angular Signals + RxJS | 7.x |
| Virtual Scrolling | Angular CDK Virtual Scroll | 17.x |
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

Implement the `PatientProfileComponent` (routed page at `/patients/:id/profile`) for the SCR-014 360-degree patient profile. The component renders a full-width patient header (name, DOB, MRN) and a horizontal Angular Material tab bar with five tabs: Summary, Timeline, Documents, Insurance, and Coding (UXR-107). Each tab loads data independently using section-based lazy loading — only the active tab triggers its API call, preventing unnecessary requests and enabling the 3-second p95 load target (AC-1, NFR-001). Every extracted data item (medication, allergy, diagnosis) shows a source link icon; clicking or hovering opens the originating document in the existing side-panel document viewer (AC-2, UXR-107). Hovering over a medication row reveals a metadata tooltip showing source document name, upload date, and extraction confidence (AC-3). AI-extracted facts display a purple "AI" badge; clinician-verified facts display a green checkmark (UXR-405). Per-tab skeleton loaders appear during fetch (SCR-014 Loading state). Per-tab empty states show contextual messages; the Summary tab empty state is "No clinical data extracted. Upload documents to populate this profile." (AC-4). Per-tab error banners with retry are shown when individual data sources fail, naming the unavailable source (Edge Case 1). Angular CDK virtual scrolling is applied within each tab section when fact counts exceed 50 items (Edge Case 2). Mobile layout collapses tabs to a scrollable horizontal strip (UXR-301, UXR-303). Full WCAG 2.1 AA accessibility: 4.5:1 contrast, keyboard navigation, visible focus rings, aria-labels on icons (UXR-201, UXR-202, UXR-204).

---

## Dependent Tasks

- **us_045/task_002** — `GET /api/v1/patients/{id}/profile` API must be implemented before integration testing.
- **us_042/task_001** — Document viewer side panel must be available for source traceability link targets (AC-2).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `PatientProfileComponent` | CREATE | Routed page: `/patients/:id/profile`; patient header + tab bar |
| `ProfileSummaryTabComponent` | CREATE | Summary tab: medications, allergies, diagnoses fact lists with source icons and AI badges |
| `ProfileTimelineTabComponent` | CREATE | Timeline tab: chronological event list; deferred to us_045 timeline story (stub with empty state) |
| `ProfileDocumentsTabComponent` | CREATE | Documents tab: links to document library (SCR-012); stub for now |
| `FactListComponent` | CREATE | Reusable component: virtual-scrollable list of ClinicalFact items with source icon, AI badge, confidence |
| `SourceTraceabilityTooltipDirective` | CREATE | Angular directive: on hover/click shows tooltip with document name, upload date, confidence |
| `PartialDataWarningComponent` | CREATE | Warning banner naming unavailable data source with retry button (Edge Case 1) |
| `PatientProfileService` | CREATE | Angular service: calls `GET /api/v1/patients/{id}/profile` via `HttpClient`; exposes `profile$` signal |
| `PatientProfileFacade` | CREATE | Facade: coordinates tab-level lazy load, tracks per-tab loading/error state with Signals |
| `ClinicalFactCardComponent` | CREATE | Fact display card: name, value, confidence badge, source icon, AI/verified indicator |
| `EmptyProfileStateComponent` | CREATE | Per-tab empty state with contextual message and CTA link |
| `PatientProfileSkeletonComponent` | CREATE | Per-tab skeleton loader matching layout of content area |
| `clinical-intelligence.routes.ts` | MODIFY | Add route: `{ path: 'patients/:id/profile', component: PatientProfileComponent }` |

---

## Implementation Plan

1. **Define route and page shell**: Create `PatientProfileComponent` as a standalone component. Add route to `clinical-intelligence.routes.ts` with `CanActivate` guard enforcing `Clinician` or `Staff` role. Render patient header (fetched via `GET /api/v1/patients/{id}`) with name, DOB, MRN using `@if`/`@else` loading pattern.
2. **Implement tabbed layout**: Use `<mat-tab-group>` with five tabs: Summary, Timeline, Documents, Insurance, Coding. On `(selectedTabChange)`, emit the selected tab to `PatientProfileFacade` to trigger lazy load for that tab only (section-based loading per Edge Case 2). Default to Summary tab on initial render.
3. **Create `PatientProfileService`**: `HttpClient.get<PatientProfileDto>('/api/v1/patients/${id}/profile?tab=summary')`. Returns `{ facts: ClinicalFactDto[], partialSources: PartialSourceDto[] }`. Expose as Signal via `toSignal()` with `initialValue: null`.
4. **Create `PatientProfileFacade`**: Tracks per-tab signals: `loadingState`, `errorState`, `data`. On tab activation, calls `PatientProfileService` for that tab's endpoint. On partial response (`partialSources.length > 0`), sets `partialWarning` signal with unavailable source names (Edge Case 1). On error, sets `errorState` with source name for retry display.
5. **Create `ProfileSummaryTabComponent`**: Renders three sections — Medications, Allergies, Diagnoses — each as a `FactListComponent`. Pass `facts` input filtered by `fact_type`. Show `PatientProfileSkeletonComponent` when loading, `EmptyProfileStateComponent` when no facts, `PartialDataWarningComponent` when partial data (Edge Case 1).
6. **Create `FactListComponent` with virtual scrolling**: Wrap fact rows in `<cdk-virtual-scroll-viewport itemSize="56">` when item count > 50 (Edge Case 2). Each row renders `ClinicalFactCardComponent`. Apply `@for (fact of facts; track fact.factId)` control flow.
7. **Create `ClinicalFactCardComponent`**: Display `name`, `value`. Conditionally show purple `AI` chip badge (`[class.ai-badge]="!fact.verified"`) and green checkmark icon (`[class.verified-badge]="fact.verified"`) per UXR-405. Show `<mat-icon>link</mat-icon>` source link button applying `SourceTraceabilityTooltipDirective`.
8. **Create `SourceTraceabilityTooltipDirective`**: On `(mouseenter)` and `(click)`, open `MatTooltip` or a `MatMenuTrigger` overlay showing: document display name, upload date (formatted via `DatePipe`), confidence percentage. On click, emit event to parent to open document viewer side panel targeting `fact.documentId` (AC-2, AC-3, UXR-107).
9. **Implement empty and error states**: `EmptyProfileStateComponent` accepts `[message]` and `[ctaLabel]` inputs. Summary tab passes: `message="No clinical data extracted. Upload documents to populate this profile."`, `ctaLabel="Upload Document"` linking to document upload (AC-4). Error state shows `PartialDataWarningComponent` with `[sourceName]` input and `(retry)` output calling `PatientProfileFacade.reloadTab()` (Edge Case 1).
10. **Accessibility and responsive**: Add `aria-label` to all icon buttons (UXR-204). Ensure `mat-tab-group` has `aria-label="Patient profile sections"`. Apply focus-visible ring via CSS (UXR-202). Verify 4.5:1 contrast ratios on AI badge and confidence indicators (UXR-201). On screens < 768px, `mat-tab-group` scrollable header (`mat-stretch-tabs="false"`) becomes horizontal scroll strip (UXR-301, UXR-303).

---

## Current Project State

```
src/
├── app/
│   ├── modules/
│   │   ├── clinical-intelligence/
│   │   │   ├── components/
│   │   │   │   ├── document-upload/              ← EXISTS (US_040)
│   │   │   │   ├── ocr-status/                   ← EXISTS (US_041)
│   │   │   │   ├── document-viewer/              ← EXISTS (US_042)
│   │   │   │   ├── document-library/             ← EXISTS (US_043)
│   │   │   │   └── patient-profile/              ← CREATE
│   │   │   │       ├── patient-profile.component.ts           ← CREATE
│   │   │   │       ├── patient-profile.component.html         ← CREATE
│   │   │   │       ├── patient-profile.component.scss         ← CREATE
│   │   │   │       ├── tabs/
│   │   │   │       │   ├── profile-summary-tab.component.ts   ← CREATE
│   │   │   │       │   ├── profile-timeline-tab.component.ts  ← CREATE
│   │   │   │       │   ├── profile-documents-tab.component.ts ← CREATE
│   │   │   │       │   ├── profile-insurance-tab.component.ts ← CREATE
│   │   │   │       │   └── profile-coding-tab.component.ts    ← CREATE
│   │   │   │       ├── fact-list/
│   │   │   │       │   ├── fact-list.component.ts             ← CREATE
│   │   │   │       │   └── clinical-fact-card.component.ts    ← CREATE
│   │   │   │       ├── shared/
│   │   │   │       │   ├── source-traceability-tooltip.directive.ts  ← CREATE
│   │   │   │       │   ├── partial-data-warning.component.ts         ← CREATE
│   │   │   │       │   ├── empty-profile-state.component.ts          ← CREATE
│   │   │   │       │   └── patient-profile-skeleton.component.ts     ← CREATE
│   │   │   │       └── index.ts                               ← CREATE
│   │   │   ├── services/
│   │   │   │   └── patient-profile.service.ts    ← CREATE
│   │   │   ├── facades/
│   │   │   │   └── patient-profile.facade.ts     ← CREATE
│   │   │   └── clinical-intelligence.routes.ts   ← MODIFY (add /patients/:id/profile route)
│   │   └── [existing modules...]
│   └── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/patient-profile.component.ts` | Routed page: patient header + mat-tab-group shell |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/patient-profile.component.html` | Template: patient header, mat-tab-group with 5 tabs |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/patient-profile.component.scss` | Styles: tab layout, AI badge, verified badge, skeleton |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/tabs/profile-summary-tab.component.ts` | Summary tab: medications, allergies, diagnoses sections |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/tabs/profile-timeline-tab.component.ts` | Timeline tab stub (empty state) |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/tabs/profile-documents-tab.component.ts` | Documents tab stub linking to document library |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/tabs/profile-insurance-tab.component.ts` | Insurance tab stub |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/tabs/profile-coding-tab.component.ts` | Coding tab stub |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/fact-list/fact-list.component.ts` | CDK virtual-scroll fact list |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/fact-list/clinical-fact-card.component.ts` | Fact card: name, value, AI/verified badge, source icon |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/shared/source-traceability-tooltip.directive.ts` | Hover/click tooltip with doc name, date, confidence |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/shared/partial-data-warning.component.ts` | Warning banner with unavailable source name and retry |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/shared/empty-profile-state.component.ts` | Per-tab empty state with message and CTA |
| CREATE | `app/modules/clinical-intelligence/components/patient-profile/shared/patient-profile-skeleton.component.ts` | Skeleton loader matching profile layout |
| CREATE | `app/modules/clinical-intelligence/services/patient-profile.service.ts` | HttpClient service calling /api/v1/patients/{id}/profile |
| CREATE | `app/modules/clinical-intelligence/facades/patient-profile.facade.ts` | Per-tab lazy load state management with Signals |
| MODIFY | `app/modules/clinical-intelligence/clinical-intelligence.routes.ts` | Add route: patients/:id/profile → PatientProfileComponent |

---

## External References

- Angular Material Tabs: https://material.angular.io/components/tabs/overview
- Angular CDK Virtual Scroll: https://material.angular.io/cdk/scrolling/overview
- Angular Signals: https://angular.dev/guide/signals
- WCAG 2.1 AA color contrast: https://www.w3.org/TR/WCAG21/#contrast-minimum
- FR-CA-002: Unified 360-degree patient profile in under 3 seconds with source traceability links
- NFR-001: 3 seconds p95 page-load time
- NFR-004: 500 concurrent active users
- UXR-107: Tabbed layout, source link icons, skeleton per tab
- UXR-405: AI-generated content distinguished with purple badge; verified with green checkmark
- UXR-201/202/204: WCAG 2.1 AA accessibility
- UXR-301/303: Responsive breakpoints; card-based layout below 768px
- SCR-014 spec: `.propel/context/docs/figma_spec.md#SCR-014`

---

## Build Commands

```bash
# Install dependencies
npm install

# Serve app locally
ng serve

# Run unit tests
ng test --include="**/patient-profile/**"

# Build production
ng build --configuration=production
```

---

## Implementation Validation Strategy

- [ ] Profile page renders at `/patients/:id/profile` with correct patient header (name, DOB, MRN)
- [ ] Tab bar shows five tabs: Summary, Timeline, Documents, Insurance, Coding
- [ ] Only the active tab makes an API call (section-based lazy loading — Network tab shows one request per tab switch)
- [ ] Source link icon is visible on every fact row (AC-2); clicking opens document viewer side panel
- [ ] Hovering over a medication row shows tooltip with document name, upload date, confidence percentage (AC-3)
- [ ] AI-extracted facts show purple "AI" badge; verified facts show green checkmark (UXR-405)
- [ ] Summary tab skeleton loader appears while data loads
- [ ] Empty state "No clinical data extracted. Upload documents to populate this profile." renders when no facts exist (AC-4)
- [ ] Partial data warning banner names unavailable source when one query fails (Edge Case 1)
- [ ] CDK virtual scroll activates when fact count > 50 within a section (Edge Case 2)
- [ ] Page renders in <3s under Lighthouse simulation (AC-1, NFR-001)
- [ ] All icon buttons have `aria-label` (UXR-204)
- [ ] Tab navigation works with keyboard only (UXR-202)
- [ ] 4.5:1 contrast ratio on AI badge and confidence text (UXR-201)
- [ ] Mobile (375px): tabs scroll horizontally instead of wrapping (UXR-301, UXR-303)

---

## Implementation Checklist

- [ ] Create `PatientProfileComponent` with patient header and `mat-tab-group` (5 tabs); add route with Clinician/Staff guard
- [ ] Create `PatientProfileFacade` with per-tab Signal state (loading, error, data, partialWarning)
- [ ] Create `PatientProfileService` calling `GET /api/v1/patients/{id}/profile`
- [ ] Create `ProfileSummaryTabComponent` with Medications, Allergies, Diagnoses sections using `FactListComponent`
- [ ] Create `FactListComponent` with CDK virtual scroll (activates at >50 items) and `@for` control flow
- [ ] Create `ClinicalFactCardComponent` with AI badge, verified checkmark, and source link icon (UXR-405)
- [ ] Create `SourceTraceabilityTooltipDirective` for hover/click tooltip + document viewer open (AC-2, AC-3)
- [ ] Create `EmptyProfileStateComponent`, `PartialDataWarningComponent`, `PatientProfileSkeletonComponent`
- [ ] Apply WCAG 2.1 AA: aria-labels, keyboard navigation, 4.5:1 contrast, focus-visible rings (UXR-201/202/204)
