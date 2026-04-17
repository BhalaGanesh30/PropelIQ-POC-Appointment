---
task_id: task_001
user_story: us_042
epic: EP-006
layer: Frontend
status: not-started
effort_hours: 8
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_042] In-Browser Document Viewer
- **Story Location**: [.propel/context/tasks/EP-006/us_042/us_042.md](.propel/context/tasks/EP-006/us_042/us_042.md)
- **Acceptance Criteria**:
  - AC-1: Given I navigate to a patient's documents, When I click on a completed document, Then the document renders in an embedded browser viewer within 3 seconds.
  - AC-2: Given the document viewer is open, When I use the zoom controls (in/out), Then the document scales smoothly between 50% and 200% zoom levels.
  - AC-3: Given the document viewer is open, When I use the rotate control, Then the document rotates 90 degrees clockwise per click.
  - AC-4: Given OCR extraction is available for the document, When I type a search term in the full-text search field, Then matching text occurrences are highlighted in the document and I can navigate between them using next/previous controls.
- **Edge Cases**:
  - Edge Case 1: Document has no OCR text available (still processing) — full-text search is disabled with message "Text extraction in progress"; zoom and rotate remain functional.
  - Edge Case 2: Large multi-page documents — lazy-loading renders pages progressively; viewer shows page count and navigation control for jumping to specific pages.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | [.propel/context/wireframes/Hi-Fi/wireframe-SCR-013-document-viewer.html](.propel/context/wireframes/Hi-Fi/wireframe-SCR-013-document-viewer.html) |
| **Screen Spec** | [figma_spec.md#SCR-013](.propel/context/docs/figma_spec.md#SCR-013) |
| **UXR Requirements** | UXR-109, UXR-201, UXR-202, UXR-204, UXR-301 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local wireframe file exists at specified path.
>
> **Note**: US_042 references SCR-023 (KPI Dashboard, EP-011). The document viewer screen is SCR-013 per figma_spec.md, which is the dedicated Document Viewer screen under EP-006.

### Screen States (SCR-013)

| State | Description |
|-------|-------------|
| Default | Document rendered in main panel (70%), toolbar with zoom/rotate/search controls pinned at top, extracted entities sidebar (30%) |
| Loading | Skeleton document placeholder during file load; must render within 3 seconds (AC-1) |
| Error | "Failed to load document" message with retry button |
| Validation | Search results highlighted in document, extracted entities listed with confidence badges |

### Layout

- Split view: document viewer (70%) + entity sidebar (30%)
- Toolbar pinned at top with zoom in/out, rotate, search, page navigation
- Mobile (375px): sidebar collapses to bottom sheet
- Tablet (768px): sidebar collapses to bottom sheet
- Desktop (1440px): full split view

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
| PDF Rendering | pdf.js (Mozilla) | 4.x |
| Image Rendering | Native HTML5 Canvas | N/A |
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

Implement the `DocumentViewerComponent` as a standalone Angular 17 component for SCR-013. The component renders clinical documents (PDF, JPG, PNG, TIFF) in-browser using Mozilla pdf.js for PDF files and native HTML5 Canvas for image files. The layout is a split view with the document panel (70%) and an extracted entities sidebar (30%), with a pinned toolbar at the top providing zoom in/out controls (50%–200% range, AC-2), rotate clockwise button (90-degree increments, AC-3), full-text search with match highlighting and next/previous navigation (AC-4), and page navigation for multi-page PDFs (Edge Case 2). The document is fetched via a pre-signed R2 URL from `GET /api/v1/documents/{id}/content` and must render within 3 seconds (AC-1). Full-text search queries `GET /api/v1/documents/{id}/search?term=` against the stored OCR `extracted_text`; when OCR is not yet available, the search field is disabled with "Text extraction in progress" (Edge Case 1). Multi-page PDFs use lazy-loading via pdf.js `getPage()` to render pages progressively (Edge Case 2). Keyboard shortcuts are supported per UXR-109: `Ctrl +/-` for zoom, `Ctrl R` for rotate, `Ctrl F` for search focus. All icon buttons have `aria-label` attributes (UXR-204). On mobile, the sidebar collapses to a bottom sheet (UXR-301).

---

## Dependent Tasks

- **us_042/task_002** — `GET /api/v1/documents/{id}/content` (pre-signed URL) and `GET /api/v1/documents/{id}/search` endpoints must be deployed (or mocked via Angular HTTP interceptor).
- **us_041/task_002** — OCR worker must populate `extracted_text` for full-text search to function.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `DocumentViewerComponent` | CREATE | Standalone component: split-view layout, PDF/image rendering, toolbar, search, sidebar |
| `DocumentViewerComponent` (template) | CREATE | Toolbar with `app-icon-button` for zoom/rotate/search/page-nav, canvas area, sidebar panel |
| `DocumentViewerComponent` (styles) | CREATE | Split view 70/30, toolbar pinned top, responsive bottom sheet for sidebar, zoom/rotate transforms |
| `DocumentViewerService` | CREATE | Angular service: `getDocumentContent(id)`, `searchDocument(id, term)`, `getDocumentMetadata(id)` |
| `PdfRendererService` | CREATE | Wrapper around pdf.js: load document, render pages, get page count, lazy-load pages |
| `ImageRendererService` | CREATE | Canvas-based image rendering with zoom/rotate transforms for JPG/PNG/TIFF |
| `DocumentSearchResult` model | CREATE | TypeScript interface: `matches: SearchMatch[]`, `totalCount: number` |
| `SearchMatch` model | CREATE | TypeScript interface: `text: string`, `pageNumber: number`, `position: number` |
| `ViewerState` model | CREATE | TypeScript interface: `zoomLevel`, `rotation`, `currentPage`, `totalPages`, `searchTerm`, `activeMatchIndex` |
| App routing module | MODIFY | Register `/documents/:id/view` route accessible to Patient, Staff, Clinician roles |

---

## Implementation Plan

1. **Create `DocumentViewerService`** in `app/features/documents/document-viewer.service.ts`: method `getDocumentContent(documentId: string): Observable<DocumentContentResponse>` wrapping `GET /api/v1/documents/{id}/content` returning `{ preSignedUrl: string, contentType: string, extractionStatus: string }`. Method `searchDocument(documentId: string, term: string): Observable<DocumentSearchResult>` wrapping `GET /api/v1/documents/{id}/search?term=`. Method `getDocumentMetadata(documentId: string): Observable<DocumentMetadata>` returning metadata including `extractionStatus` and `pageCount`.
2. **Create `PdfRendererService`** in `app/features/documents/pdf-renderer.service.ts`: initialize pdf.js with the pre-signed URL via `pdfjsLib.getDocument(url)`. Method `loadDocument(url: string): Promise<PDFDocumentProxy>` returning the PDF proxy. Method `renderPage(pageNumber: number, canvas: HTMLCanvasElement, scale: number, rotation: number): Promise<void>` renders a single page at the given scale and rotation. Method `getPageCount(): number`. Implement lazy-loading: only render the current page and 1 page ahead/behind (Edge Case 2).
3. **Create `ImageRendererService`** in `app/features/documents/image-renderer.service.ts`: load image from pre-signed URL into an `HTMLImageElement`. Method `renderImage(image: HTMLImageElement, canvas: HTMLCanvasElement, scale: number, rotation: number): void` draws the image on canvas with CSS transform for zoom and canvas rotation for rotate.
4. **Implement toolbar**: Pinned toolbar at the top using `app-icon-button` components:
   - **Zoom in** (`+` icon, `aria-label="Zoom in"`): increment `zoomLevel` by 25%, max 200%. Keyboard: `Ctrl +`.
   - **Zoom out** (`-` icon, `aria-label="Zoom out"`): decrement `zoomLevel` by 25%, min 50%. Keyboard: `Ctrl -`.
   - **Zoom display**: Show current zoom percentage as text label.
   - **Rotate** (rotate icon, `aria-label="Rotate clockwise"`): increment `rotation` by 90 degrees (mod 360). Keyboard: `Ctrl R`.
   - **Search field** (`mat-form-field` with search icon): `(input)` event debounced at 300ms calls `searchDocument()`. Display match count "N of M". Next/Previous buttons navigate between matches. Keyboard: `Ctrl F` focuses the field. Disabled with placeholder "Text extraction in progress" when `extractionStatus !== 'Completed'` (Edge Case 1).
   - **Page navigation**: "Page X of Y" display with previous/next buttons and a page number input for direct jump (Edge Case 2). Hidden for single-page documents and image files.
5. **Implement document rendering area**: A `<canvas>` element fills the document panel (70% width). On component init, determine file type from `contentType`: if `application/pdf`, use `PdfRendererService`; if `image/*`, use `ImageRendererService`. Apply zoom and rotation via the respective service methods. Render within 3 seconds — use a loading skeleton placeholder during fetch (AC-1).
6. **Implement search highlighting**: When search returns matches, for PDF files, use pdf.js text layer to highlight matching text spans with a yellow background. For image files, overlay highlights are not possible — display matches in the sidebar with page references. Navigate between matches with next/previous, scrolling to the match position and updating `activeMatchIndex`.
7. **Implement extracted entities sidebar**: Right panel (30% width on desktop) displays extracted entities from the document (read from document metadata or a separate endpoint). On mobile and tablet, collapse to a `mat-bottom-sheet` triggered by a sidebar toggle button. Show entity name, value, and confidence badge per entity.
8. **Register route**: Add `/documents/:id/view` route accessible to Patient, Staff, Clinician roles. The route resolves `documentId` from the URL parameter.

---

## Current Project State

```
app/
├── features/
│   ├── documents/                                    ← MODIFY (this task)
│   │   ├── document-upload.component.ts              ← EXISTS (US_040/US_041)
│   │   ├── document-upload.service.ts                ← EXISTS (US_040/US_041)
│   │   ├── document-viewer.component.ts              ← CREATE
│   │   ├── document-viewer.component.html            ← CREATE
│   │   ├── document-viewer.component.scss            ← CREATE
│   │   ├── document-viewer.service.ts                ← CREATE
│   │   ├── pdf-renderer.service.ts                   ← CREATE
│   │   └── image-renderer.service.ts                 ← CREATE
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── document-search-result.model.ts           ← CREATE
│       ├── search-match.model.ts                     ← CREATE
│       ├── viewer-state.model.ts                     ← CREATE
│       └── [existing models...]
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual feature folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/documents/document-viewer.component.ts` | Standalone component: split-view, PDF/image rendering, toolbar, zoom, rotate, search, page nav |
| CREATE | `app/features/documents/document-viewer.component.html` | Template: toolbar with `app-icon-button`, canvas area, sidebar, search field, page navigation |
| CREATE | `app/features/documents/document-viewer.component.scss` | Split view (70/30), toolbar pinned, responsive bottom sheet, zoom/rotate transforms, search highlights |
| CREATE | `app/features/documents/document-viewer.service.ts` | Service: `getDocumentContent()`, `searchDocument()`, `getDocumentMetadata()` |
| CREATE | `app/features/documents/pdf-renderer.service.ts` | pdf.js wrapper: load, render page, lazy-load, page count |
| CREATE | `app/features/documents/image-renderer.service.ts` | Canvas-based image rendering with zoom/rotate transforms |
| CREATE | `app/shared/models/document-search-result.model.ts` | `DocumentSearchResult` interface |
| CREATE | `app/shared/models/search-match.model.ts` | `SearchMatch` interface |
| CREATE | `app/shared/models/viewer-state.model.ts` | `ViewerState` interface: zoomLevel, rotation, currentPage, totalPages, searchTerm, activeMatchIndex |
| MODIFY | `app/app.routes.ts` | Add `/documents/:id/view` route for Patient, Staff, Clinician roles |

---

## External References

- Mozilla pdf.js: https://mozilla.github.io/pdf.js/
- pdf.js Angular integration guide: https://github.com/nickvdyck/pdfjs-dist
- HTML5 Canvas API: https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API
- Angular Material Bottom Sheet: https://material.angular.io/components/bottom-sheet/overview
- FR-DM-003: System MUST provide in-browser viewing with zoom, rotate, and full-text search over extracted content
- UXR-109: Document viewer MUST support zoom, rotate, and full-text search with keyboard shortcuts
- UXR-201: WCAG 2.1 AA colour contrast ratio of at least 4.5:1
- UXR-202: Full keyboard navigation with visible focus indicators
- UXR-204: All images and icons MUST have alt text or aria-label attributes describing their purpose
- UXR-301: Mobile (375px), tablet (768px), and desktop (1440px) breakpoints
- SCR-013 wireframe: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-013-document-viewer.html`

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

- [ ] Unit tests pass for `DocumentViewerComponent` (rendering, zoom, rotate, search, page navigation)
- [ ] Unit tests pass for `PdfRendererService` (pdf.js load, render page, lazy-load)
- [ ] Unit tests pass for `ImageRendererService` (canvas draw, zoom transform, rotation)
- [ ] Unit tests pass for `DocumentViewerService` (HTTP calls mocked)
- [ ] **[UI Task]** Visual comparison against wireframe at 375px, 768px, 1440px
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Document renders within 3 seconds from navigation (AC-1)
- [ ] Zoom scales smoothly between 50% and 200% (AC-2)
- [ ] Rotate increments by 90 degrees clockwise per click (AC-3)
- [ ] Search highlights matching text and supports next/previous navigation (AC-4)
- [ ] Search disabled with "Text extraction in progress" when OCR not complete (Edge Case 1)
- [ ] Multi-page PDFs lazy-load pages with page navigation controls (Edge Case 2)
- [ ] Keyboard shortcuts work: `Ctrl +/-` zoom, `Ctrl R` rotate, `Ctrl F` search (UXR-109)
- [ ] All icon buttons have `aria-label` attributes (UXR-204)
- [ ] Sidebar collapses to bottom sheet on mobile/tablet (UXR-301)

---

## Implementation Checklist

- [ ] Create `DocumentViewerService` wrapping content, search, and metadata GET endpoints
- [ ] Create `PdfRendererService` with pdf.js: load document, render pages with zoom/rotation, lazy-load adjacent pages (Edge Case 2)
- [ ] Create `ImageRendererService` with Canvas: draw image with zoom/rotation transforms for JPG/PNG/TIFF
- [ ] Implement toolbar with `app-icon-button` zoom in/out (50%–200%), rotate (90-degree CW), search field, page navigation (UXR-109)
- [ ] Implement search highlighting with match count, next/previous navigation; disable when OCR unavailable (AC-4, Edge Case 1)
- [ ] Implement split-view layout (70/30) with sidebar collapsing to bottom sheet on mobile (UXR-301)
- [ ] Add keyboard shortcuts: `Ctrl +/-` zoom, `Ctrl R` rotate, `Ctrl F` search focus; add `aria-label` to all icons (UXR-109, UXR-204)
- [ ] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation
