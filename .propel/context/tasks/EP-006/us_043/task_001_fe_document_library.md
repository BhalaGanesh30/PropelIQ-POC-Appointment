---
task_id: task_001
user_story: us_043
epic: EP-006
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_043] Document Categorization, Rename, and Soft-Delete
- **Story Location**: [.propel/context/tasks/EP-006/us_043/us_043.md](.propel/context/tasks/EP-006/us_043/us_043.md)
- **Acceptance Criteria**:
  - AC-1: Given I have access to a patient's document list, When I assign a category to a document (e.g., Lab Report, Referral, Prescription), Then the category is saved and the document list updates to show the assigned category label.
  - AC-2: Given I view a document in the document list, When I rename it, Then the display name is updated immediately in the UI and persisted to the database, while the original storage filename is preserved.
  - AC-3: Given I want to remove a document from view, When I soft-delete a document (after confirming the confirmation dialog), Then the document is hidden from the active list but remains in the database with `IsDeleted = true`.
  - AC-4: Given I am an admin reviewing soft-deleted documents, When I access the document trash view, Then all soft-deleted documents are listed with their deletion date and I can restore them.
- **Edge Cases**:
  - Edge Case 1: Categorizing a document still processing (OCR in progress) — categorization is allowed; category is saved; OCR completion does not override the category.
  - Edge Case 2: Hard deletion prevention — no hard delete exposed in any UI; only soft-delete is permitted.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | [.propel/context/wireframes/Hi-Fi/wireframe-SCR-012-document-library.html](.propel/context/wireframes/Hi-Fi/wireframe-SCR-012-document-library.html) |
| **Screen Spec** | [figma_spec.md#SCR-012](.propel/context/docs/figma_spec.md#SCR-012) |
| **UXR Requirements** | UXR-111, UXR-201, UXR-202, UXR-206, UXR-301 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local wireframe file exists at specified path.
>
> **Note**: US_043 references SCR-024 (Template Editor, EP-010). The document library screen is SCR-012 per figma_spec.md, which is the dedicated Document Library screen under EP-006 with category tags, rename, and delete actions.

### Screen States (SCR-012)

| State | Description |
|-------|-------------|
| Default | Filter bar (category, date, status), document table with columns: name, category, date, status, actions (view, rename, delete) |
| Loading | Skeleton rows during fetch |
| Empty | Illustration with "No documents" and upload CTA |
| Error | Retry banner on load failure |
| Validation | Confirmation dialog for soft-delete, success toast for rename |

### Layout

- Full-width `app-data-table` on desktop with columns: name, category badge, upload date, processing status, actions menu
- Card layout on mobile (375px) via `app-data-table[variant="Card-on-mobile"]`
- `app-pagination` below the table
- Filter bar above the table with category dropdown, date range picker, status filter

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
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

Implement the `DocumentLibraryComponent` as a standalone Angular 17 component for SCR-012. The component renders a filterable, paginated document list for a patient using `app-data-table` with columns for document name, category badge, upload date, processing status, and an actions menu. The filter bar provides category dropdown, date range picker, and status filter. Actions per document include: (1) **Categorize** — inline category dropdown or modal to assign a category from a predefined list (Lab Report, Referral, Prescription, Imaging, Insurance, Other), updating immediately in the table (AC-1); (2) **Rename** — inline edit or modal to update the display name, with optimistic UI update and persistence via API (AC-2); (3) **Soft-Delete** — triggers `app-confirm-dialog[variant="Destructive"]` with UXR-111 confirmation, then hides the document from the active list (AC-3). An admin-only "Trash" tab or toggle displays soft-deleted documents with deletion dates and a "Restore" action (AC-4). Focus is trapped within dialogs and returned to the trigger element on close (UXR-206). Categorization is allowed even for documents with OCR in progress (Edge Case 1). No hard-delete action exists in the UI (Edge Case 2).

---

## Dependent Tasks

- **us_043/task_002** — Categorize, rename, soft-delete, restore, and list endpoints must be deployed (or mocked via Angular HTTP interceptor).
- **us_043/task_003** — `display_name`, `is_deleted`, `deleted_at`, `category` enum migration must be applied.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `DocumentLibraryComponent` | CREATE | Standalone component: filter bar, data table, actions menu, trash toggle |
| `DocumentLibraryComponent` (template) | CREATE | `app-data-table`, filter bar, category dropdown, rename modal, `app-confirm-dialog`, `app-pagination` |
| `DocumentLibraryComponent` (styles) | CREATE | Full-width table desktop, card-on-mobile, category badge colours, filter bar layout |
| `DocumentLibraryService` | CREATE | Angular service: list, categorize, rename, soft-delete, restore, list-trash |
| `DocumentListItem` model | CREATE | TypeScript interface: `documentId`, `displayName`, `category`, `uploadedAt`, `extractionStatus`, `scanResult`, `isDeleted`, `deletedAt` |
| `DocumentCategory` enum | CREATE | TypeScript enum: `LabReport`, `Referral`, `Prescription`, `Imaging`, `Insurance`, `Other` |
| `DocumentListFilter` model | CREATE | TypeScript interface: `category`, `dateFrom`, `dateTo`, `status`, `includeDeleted` |
| App routing module | MODIFY | Register `/documents/library` route for Clinician and Staff; register `/documents/trash` for Admin |

---

## Implementation Plan

1. **Create `DocumentCategory` enum** in `app/shared/models/document-category.enum.ts`: values `LabReport = 'lab_report'`, `Referral = 'referral'`, `Prescription = 'prescription'`, `Imaging = 'imaging'`, `Insurance = 'insurance'`, `Other = 'other'`.
2. **Create `DocumentListItem` and `DocumentListFilter` models**: `DocumentListItem` with `documentId: string`, `displayName: string`, `originalFilename: string`, `category: DocumentCategory | null`, `uploadedAt: string`, `extractionStatus: string`, `scanResult: string`, `isDeleted: boolean`, `deletedAt: string | null`. `DocumentListFilter` with `category: DocumentCategory | null`, `dateFrom: string | null`, `dateTo: string | null`, `status: string | null`, `includeDeleted: boolean`.
3. **Create `DocumentLibraryService`** in `app/features/documents/document-library.service.ts`:
   - `listDocuments(patientId: string, filter: DocumentListFilter, page: number, pageSize: number): Observable<PaginatedResponse<DocumentListItem>>` wrapping `GET /api/v1/documents?patientId=&category=&dateFrom=&dateTo=&status=&includeDeleted=&page=&pageSize=`.
   - `categorize(documentId: string, category: DocumentCategory): Observable<void>` wrapping `PATCH /api/v1/documents/{id}/category`.
   - `rename(documentId: string, displayName: string): Observable<void>` wrapping `PATCH /api/v1/documents/{id}/rename`.
   - `softDelete(documentId: string): Observable<void>` wrapping `DELETE /api/v1/documents/{id}`.
   - `restore(documentId: string): Observable<void>` wrapping `POST /api/v1/documents/{id}/restore`.
4. **Implement filter bar**: Above the table, render a horizontal filter bar with: (a) `mat-select` for category (all categories + "All" default); (b) `mat-date-range-input` for date range; (c) `mat-select` for processing status (Queued, Processing, Completed, Failed, All). Filters emit via `(selectionChange)` and update the document list via `DocumentLibraryService.listDocuments()`.
5. **Implement document table**: Use `app-data-table` with columns:
   - **Name**: Display name with click-to-open viewer link. Show original filename in tooltip if different from display name.
   - **Category**: `app-badge` with category label and colour. Click triggers inline `mat-select` dropdown for category assignment (AC-1). Enabled even for documents with OCR in progress (Edge Case 1).
   - **Uploaded**: Formatted date (`medium` pipe).
   - **Status**: `app-badge` for `extractionStatus` with colour semantics (green=Completed, blue=Processing, grey=Queued, red=Failed).
   - **Actions**: `mat-menu` with "Rename", "Delete" options. No "Hard Delete" option (Edge Case 2).
   Mobile: `app-data-table[variant="Card-on-mobile"]` renders each document as a card.
6. **Implement rename action**: "Rename" in actions menu opens a `mat-dialog` with a text input pre-filled with the current display name. On submit, call `documentLibraryService.rename()` with optimistic UI update — immediately update the table row, revert on API error (AC-2). Focus returns to the actions trigger on dialog close (UXR-206).
7. **Implement soft-delete action**: "Delete" in actions menu opens `app-confirm-dialog[variant="Destructive"]` with message "Are you sure you want to delete {displayName}? This document will be moved to trash." (UXR-111). Focus is trapped within the dialog (UXR-206). On confirm, call `documentLibraryService.softDelete()`, remove the row from the active list with a fade-out animation, and show a success toast "Document moved to trash" with an "Undo" action (3-second timer). On undo, call `restore()`.
8. **Implement trash view**: A "Trash" tab/toggle (visible to Admin role only) switches `includeDeleted = true` filter. Trash view shows soft-deleted documents with `deletedAt` date column and a "Restore" action button. On restore, call `documentLibraryService.restore()` and move the document back to the active list (AC-4). Add `app-pagination` below the table for both active and trash views.

---

## Current Project State

```
app/
├── features/
│   ├── documents/                                    ← MODIFY (this task)
│   │   ├── document-upload.component.ts              ← EXISTS (US_040/US_041)
│   │   ├── document-viewer.component.ts              ← EXISTS (US_042)
│   │   ├── document-library.component.ts             ← CREATE
│   │   ├── document-library.component.html           ← CREATE
│   │   ├── document-library.component.scss           ← CREATE
│   │   └── document-library.service.ts               ← CREATE
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── document-list-item.model.ts               ← CREATE
│       ├── document-list-filter.model.ts             ← CREATE
│       ├── document-category.enum.ts                 ← CREATE
│       └── [existing models...]
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual feature folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/documents/document-library.component.ts` | Standalone component: filter bar, data table, categorize, rename, soft-delete, trash toggle |
| CREATE | `app/features/documents/document-library.component.html` | Template: `app-data-table`, filter bar, category dropdown, rename dialog, `app-confirm-dialog`, `app-pagination` |
| CREATE | `app/features/documents/document-library.component.scss` | Full-width table, card-on-mobile, category badge colours, filter bar, trash view styling |
| CREATE | `app/features/documents/document-library.service.ts` | Service: list, categorize, rename, soft-delete, restore |
| CREATE | `app/shared/models/document-list-item.model.ts` | `DocumentListItem` interface |
| CREATE | `app/shared/models/document-list-filter.model.ts` | `DocumentListFilter` interface |
| CREATE | `app/shared/models/document-category.enum.ts` | `DocumentCategory` enum |
| MODIFY | `app/app.routes.ts` | Add `/documents/library` route (Clinician, Staff) and `/documents/trash` route (Admin) |

---

## External References

- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- Angular Material Menu: https://material.angular.io/components/menu/overview
- Angular Material Date Range Picker: https://material.angular.io/components/datepicker/overview
- WAI-ARIA dialog focus trapping: https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- FR-DM-004: System MUST support document categorization, rename, and soft-delete operations
- UXR-111: All destructive actions MUST require confirmation dialog
- UXR-201: WCAG 2.1 AA colour contrast ratio of at least 4.5:1
- UXR-202: Full keyboard navigation with visible focus indicators
- UXR-206: Focus MUST be trapped within modal dialogs and returned to the trigger element on close
- UXR-301: Mobile (375px), tablet (768px), and desktop (1440px) breakpoints
- SCR-012 wireframe: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-012-document-library.html`

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

- [ ] Unit tests pass for `DocumentLibraryComponent` (table render, filter, categorize, rename, delete, restore)
- [ ] Unit tests pass for `DocumentLibraryService` (HTTP calls mocked)
- [ ] **[UI Task]** Visual comparison against wireframe at 375px, 768px, 1440px
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Category assignment updates table immediately and persists (AC-1)
- [ ] Categorization allowed for documents with OCR in progress (Edge Case 1)
- [ ] Rename updates display name optimistically; reverts on error (AC-2)
- [ ] Soft-delete shows destructive confirmation dialog (UXR-111), hides document from active list (AC-3)
- [ ] No hard-delete action exposed anywhere in the UI (Edge Case 2)
- [ ] Trash view lists soft-deleted documents with deletion date; restore works (AC-4)
- [ ] Focus trapped in dialogs and returned to trigger on close (UXR-206)
- [ ] Table renders as cards on mobile (UXR-301)

---

## Implementation Checklist

- [X] Create `DocumentCategory` enum and `DocumentListItem`, `DocumentListFilter` models
- [X] Create `DocumentLibraryService` wrapping list, categorize, rename, soft-delete, restore endpoints
- [X] Implement filter bar with category dropdown, date range picker, and status filter
- [X] Implement `app-data-table` with name, category badge, date, status, and actions columns; card-on-mobile (UXR-301)
- [X] Implement inline category assignment via `mat-select` dropdown (AC-1, Edge Case 1)
- [X] Implement rename via `mat-dialog` with optimistic UI update (AC-2); focus return to trigger (UXR-206)
- [X] Implement soft-delete via `app-confirm-dialog[variant="Destructive"]` (AC-3, UXR-111); undo toast with 3-second timer
- [X] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation
