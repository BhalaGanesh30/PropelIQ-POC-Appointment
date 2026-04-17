---
task_id: task_001
user_story: us_039
epic: EP-005
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_039] Insurance Verification Report and Export
- **Story Location**: [.propel/context/tasks/EP-005/us_039/us_039.md](.propel/context/tasks/EP-005/us_039/us_039.md)
- **Acceptance Criteria**:
  - AC-1: Given I am authenticated as a staff member, When I navigate to the insurance verification report, Then all patient insurance records are displayed with their validation status (SoftValidated, ValidationFailed, ValidationPending).
  - AC-2: Given the verification report is displayed, When I apply a status filter (e.g., ValidationFailed), Then only records matching the selected status are shown within 500 ms.
  - AC-3: Given I filter the report, When I click "Export PDF," Then the filtered records export as a PDF within 5 seconds with patient name, insurance provider, policy number, and validation status.
  - AC-4: Given I click "Export CSV," When the export is processed, Then a CSV file downloads with the same data fields suitable for import into a billing system.
- **Edge Cases**:
  - Edge Case 1: Report contains thousands of records — server-side pagination is applied; exports include all filtered records regardless of the current page view.
  - Edge Case 2: Patient role attempts access — receives HTTP 403; component guards the route with staff-only role check.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-028-insurance-report.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | [figma_spec.md#SCR-028](.propel/context/docs/figma_spec.md#SCR-028) |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-303, UXR-404 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **PENDING**: UI-impacting task awaiting wireframe (provide file or URL).
>
> **Note**: US_039 references SCR-021 (Audit Log Viewer, EP-010, Admin-only). The insurance verification report is part of SCR-028 (Insurance Verification, EP-005) per figma_spec.md, described as "Verification report accessible via link below form." No dedicated wireframe exists for the report sub-view; the SCR-028 wireframe covers the parent form.

### Screen States (Insurance Verification Report — sub-view of SCR-028)

| State | Description |
|-------|-------------|
| Default | Filter bar (status dropdown: All, SoftValidated, ValidationFailed, ValidationPending), data table with columns: patient name, provider, policy number, status badge, date. Export PDF and Export CSV buttons. Server-side pagination. |
| Loading | Skeleton rows during data fetch, progress indicator during export generation |
| Empty | "No insurance records match the selected filter" with clear-filter CTA |
| Error | Retry banner on load failure; export error toast with retry |
| Validation | Applied filter shown as removable chip above table; export success toast |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
| Data Table | Angular Material Table (`mat-table`) | 17.x |
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

Implement the `InsuranceVerificationReportComponent` as a standalone Angular 17 component providing a staff-only data table view of all patient insurance verification records. The component uses `app-data-table` (Angular Material `mat-table` with `mat-sort` and server-side `mat-paginator`) displaying columns: Patient Name, Insurance Provider, Policy Number, Validation Status (colour-coded `app-badge` per UXR-404: green for SoftValidated, amber for ValidationPending, red for ValidationFailed), and Validated Date. A filter bar above the table contains a status dropdown (`mat-select`) with options All, SoftValidated, ValidationFailed, and ValidationPending. Selecting a filter triggers `GET /api/v1/insurance/verification-report?status={status}&page={page}&pageSize={size}` with the result rendered within 500 ms (AC-2). Applied filters display as removable chips above the table. Two export buttons ("Export PDF" and "Export CSV") call the respective export endpoints. PDF export downloads a generated PDF file (AC-3); CSV export downloads a CSV file (AC-4). Exports include all filtered records regardless of the current page (Edge Case 1). The route is guarded with a `canActivate` role check for `Staff` and `Admin` roles only — `Patient` role is redirected with 403 handling (Edge Case 2). On screens below 768px, the data table switches to card-based layout per UXR-303. The component uses `app-pagination` with numbered pages and previous/next navigation.

---

## Dependent Tasks

- **us_039/task_002** — `GET /api/v1/insurance/verification-report`, `GET /api/v1/insurance/verification-report/export/pdf`, and `GET /api/v1/insurance/verification-report/export/csv` endpoints must be deployed (or mocked via Angular HTTP interceptor).
- **us_037/task_001** — `InsuranceValidationFormComponent` must exist; the report is accessible via a link below the insurance form (SCR-028 layout).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `InsuranceVerificationReportComponent` | CREATE | Standalone Angular 17 component — data table, filters, export, pagination |
| `InsuranceVerificationReportComponent` (template) | CREATE | `app-data-table` with `mat-sort`, status filter `mat-select`, export buttons, `app-pagination` |
| `InsuranceVerificationReportComponent` (styles) | CREATE | Table layout, status badge colours, card-based mobile layout, responsive breakpoints |
| `InsuranceReportService` | CREATE | Angular service wrapping report listing and export endpoints |
| `InsuranceVerificationRecord` model | CREATE | TypeScript interface: `patientName`, `providerName`, `policyNumber`, `validationStatus`, `validatedAt` |
| `VerificationReportPagedResult` model | CREATE | TypeScript interface: `records[]`, `totalCount`, `page`, `pageSize` |
| Staff routing module | MODIFY | Register `/staff/insurance/report` route with `canActivate` staff role guard |
| `InsuranceValidationFormComponent` | MODIFY | Add "View Verification Report" link below the form (SCR-028 layout) |

---

## Implementation Plan

1. **Create `InsuranceReportService`** in `app/features/insurance/insurance-report.service.ts`: method `getReport(status: string | null, page: number, pageSize: number): Observable<VerificationReportPagedResult>` wrapping `HttpClient.get('/api/v1/insurance/verification-report', { params })`. Method `exportPdf(status: string | null): Observable<Blob>` wrapping `HttpClient.get('/api/v1/insurance/verification-report/export/pdf', { params, responseType: 'blob' })`. Method `exportCsv(status: string | null): Observable<Blob>` wrapping `HttpClient.get('/api/v1/insurance/verification-report/export/csv', { params, responseType: 'blob' })`.
2. **Implement data table**: Use `app-data-table` (`mat-table`) with columns: Patient Name, Insurance Provider, Policy Number, Validation Status, Validated Date. Enable `mat-sort` on all columns with server-side sorting. Render Validation Status as colour-coded `app-badge`: green `app-badge[variant="success"]` for SoftValidated, amber `app-badge[variant="warning"]` for ValidationPending, red `app-badge[variant="error"]` for ValidationFailed (UXR-404). On mobile (<768px), switch to card-based layout per UXR-303.
3. **Implement status filter**: Add `mat-select` dropdown in the filter bar with options: "All Statuses", "Soft Validated", "Validation Failed", "Validation Pending". On selection change, call `insuranceReportService.getReport(selectedStatus, 1, pageSize)`. Show applied filter as a removable `mat-chip` above the table. Target sub-500ms rendering by relying on the API's Redis-cached response (AC-2).
4. **Implement server-side pagination**: Use `app-pagination` with numbered pages and prev/next. On page change, call `insuranceReportService.getReport(currentStatus, newPage, pageSize)`. Default `pageSize = 25`. Show skeleton rows during fetch.
5. **Implement PDF export**: On "Export PDF" button click, show loading spinner on the button (UXR-501 pattern). Call `insuranceReportService.exportPdf(currentStatus)`. On response, trigger browser file download via `URL.createObjectURL(blob)` with filename `insurance-verification-report.pdf`. Export includes all filtered records, not just the current page (Edge Case 1).
6. **Implement CSV export**: On "Export CSV" button click, same loading/download pattern. Call `insuranceReportService.exportCsv(currentStatus)`. Download as `insurance-verification-report.csv`. CSV columns: Patient Name, Insurance Provider, Policy Number, Validation Status, Validated Date (AC-4).
7. **Guard route for staff only**: Add `canActivate: [RoleGuard]` with `data: { roles: ['Staff', 'Admin'] }` to the route definition. On `Patient` role attempt, redirect to a 403 forbidden page (Edge Case 2).
8. **Add report link to SCR-028**: In `InsuranceValidationFormComponent`, add a `routerLink` to `/staff/insurance/report` below the form — "View Verification Report" link per SCR-028 layout description.

---

## Current Project State

```
app/
├── features/
│   ├── insurance/
│   │   ├── insurance-validation-form.component.*     ← EXISTS (us_037/task_001) — MODIFY (add report link)
│   │   ├── insurance.service.ts                      ← EXISTS (us_037/task_001)
│   │   ├── insurance-report.service.ts               ← CREATE
│   │   ├── insurance-verification-report.component.ts    ← CREATE
│   │   ├── insurance-verification-report.component.html  ← CREATE
│   │   └── insurance-verification-report.component.scss  ← CREATE
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── insurance-verification-record.model.ts    ← CREATE
│       ├── verification-report-paged-result.model.ts ← CREATE
│       └── [existing models...]
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual feature folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/insurance/insurance-verification-report.component.ts` | Standalone component: data table, status filter, export buttons, pagination |
| CREATE | `app/features/insurance/insurance-verification-report.component.html` | Template: `app-data-table`, `mat-select` filter, `mat-chip`, export buttons, `app-pagination` |
| CREATE | `app/features/insurance/insurance-verification-report.component.scss` | Table layout, status badge colours, card-based mobile layout (<768px) |
| CREATE | `app/features/insurance/insurance-report.service.ts` | Service wrapping report GET, PDF export, CSV export endpoints |
| CREATE | `app/shared/models/insurance-verification-record.model.ts` | `InsuranceVerificationRecord` interface |
| CREATE | `app/shared/models/verification-report-paged-result.model.ts` | `VerificationReportPagedResult` interface |
| MODIFY | `app/app.routes.ts` (or staff routing) | Add `/staff/insurance/report` route with staff role guard |
| MODIFY | `app/features/insurance/insurance-validation-form.component.html` | Add "View Verification Report" routerLink below form |

---

## External References

- Angular Material Table: https://material.angular.io/components/table/overview
- Angular Material Sort: https://material.angular.io/components/sort/overview
- Angular Material Paginator: https://material.angular.io/components/paginator/overview
- Angular Material Select: https://material.angular.io/components/select/overview
- Angular Material Chips: https://material.angular.io/components/chips/overview
- Blob download pattern in Angular: https://angular.dev/guide/http/making-requests#requesting-non-json-data
- FR-IP-003: System MUST provide insurance verification reports with status filters and export capability
- NFR-002: API response within 500 ms p95
- UXR-201: WCAG 2.1 AA colour contrast ratio of at least 4.5:1
- UXR-202: Full keyboard navigation with visible focus indicators
- UXR-301: Mobile (375px), tablet (768px), and desktop (1440px) breakpoints
- UXR-303: Data tables MUST switch to card-based layout on screens below 768px
- UXR-404: Status indicators MUST use consistent colour semantics: green (success), amber (warning), red (error)

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

- [ ] Unit tests pass for `InsuranceVerificationReportComponent` (table rendering, filter, pagination, export triggers)
- [ ] Unit tests pass for `InsuranceReportService` (HTTP calls mocked)
- [ ] **[UI Task]** Visual comparison at 375px (card layout), 768px, 1440px (full table)
- [ ] Status badges render with correct colours: green (SoftValidated), amber (ValidationPending), red (ValidationFailed) per UXR-404
- [ ] Filter selection updates table within 500 ms (AC-2)
- [ ] PDF export downloads file with correct content (AC-3)
- [ ] CSV export downloads file suitable for billing import (AC-4)
- [ ] Staff role guard blocks Patient role with 403 redirect (Edge Case 2)
- [ ] Exports include all filtered records regardless of page (Edge Case 1)
- [ ] Card-based layout renders on screens below 768px (UXR-303)

---

## Implementation Checklist

- [ ] Create `InsuranceReportService` wrapping report listing, PDF export, and CSV export API endpoints
- [ ] Implement `app-data-table` with `mat-sort` displaying patient name, provider, policy number, status badge, and date columns
- [ ] Implement status filter dropdown (`mat-select`) with removable chip indicator and sub-500ms table update (AC-2)
- [ ] Implement server-side pagination with `app-pagination` (default 25 per page) and skeleton loading
- [ ] Implement PDF and CSV export buttons with blob download and loading state; exports include all filtered records (Edge Case 1)
- [ ] Implement card-based layout for mobile screens below 768px (UXR-303) and colour-coded status badges (UXR-404)
- [ ] Guard route with staff-only `canActivate` role check; redirect Patient role to 403 page (Edge Case 2)
- [ ] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation (when available)
