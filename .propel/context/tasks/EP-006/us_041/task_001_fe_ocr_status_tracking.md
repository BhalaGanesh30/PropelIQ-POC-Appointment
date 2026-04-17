---
task_id: task_001
user_story: us_041
epic: EP-006
layer: Frontend
status: not-started
effort_hours: 5
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_041] Async OCR Processing and Status Tracking
- **Story Location**: [.propel/context/tasks/EP-006/us_041/us_041.md](.propel/context/tasks/EP-006/us_041/us_041.md)
- **Acceptance Criteria**:
  - AC-1: Given a clean document is successfully uploaded, When the file is persisted, Then an OCR processing job is queued and the document record is updated with status "Queued."
  - AC-2: Given an OCR job is queued, When the background worker processes it using Tesseract, Then the document status transitions through "Processing" → "Completed" and the extracted text is stored against the document record.
  - AC-3: Given OCR processing completes, When I check the document status, Then the status shows "Completed" and the extracted text is available within 2 minutes p95 for files up to 10 MB.
  - AC-4: Given an OCR job fails, When the failure is detected, Then the document status is updated to "Failed," the error is logged, and the job is retried up to 3 times with exponential backoff before moving to the dead-letter queue.
- **Edge Cases**:
  - Edge Case 1: Low text quality scanned image — OCR produces low-confidence extraction; document flagged for manual review with raw OCR output stored. UI displays "Manual Review Required" badge.
  - Edge Case 2: Concurrent OCR jobs — queue workers process in parallel up to configured concurrency limit; UI reflects accurate per-document status independently.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | [.propel/context/wireframes/Hi-Fi/wireframe-SCR-011-document-upload.html](.propel/context/wireframes/Hi-Fi/wireframe-SCR-011-document-upload.html) |
| **Screen Spec** | [figma_spec.md#SCR-011](.propel/context/docs/figma_spec.md#SCR-011) |
| **UXR Requirements** | UXR-203, UXR-404, UXR-501 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local wireframe file exists at specified path.
>
> **Note**: US_041 references SCR-022 (Compliance Reports, EP-010). The OCR processing status is displayed on SCR-011 (Document Upload, EP-006) per figma_spec.md, which shows processing status badges in the Validation state and an OCR processing spinner in the Loading state.

### Screen States (SCR-011 — OCR Status Context)

| State | Description |
|-------|-------------|
| Loading | OCR processing spinner per document after upload and malware scan complete |
| Validation | Processing status badges: "Queued" (grey), "Processing" (blue spinner), "Completed" (green check), "Failed" (red with retry button) |
| Error | OCR failure with retry button; "Manual Review Required" badge for low-confidence extractions |

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

Extend the `DocumentUploadComponent` (created in US_040/task_001) to display OCR processing status after a successful upload and malware scan. The component polls `GET /api/v1/documents/{id}/status` (created in US_040/task_002) to track the `extraction_status` field through its lifecycle: "Queued" → "Processing" → "Completed" or "Failed". Status badges use consistent colour semantics per UXR-404: grey for "Queued", blue spinner for "Processing", green check for "Completed", red for "Failed" with a retry button. When OCR completes, the extracted text preview is available via an expandable panel. For failed jobs, a retry button triggers `POST /api/v1/documents/{id}/retry-ocr`. Screen reader announcements are provided for dynamic status transitions via `aria-live="polite"` regions (UXR-203). Low-confidence extractions display a "Manual Review Required" amber badge (Edge Case 1). The component handles concurrent document status independently per file row.

---

## Dependent Tasks

- **us_040/task_001** — `DocumentUploadComponent` and `DocumentUploadService` must exist as the base component this task extends.
- **us_041/task_002** — `POST /api/v1/documents/{id}/retry-ocr` endpoint must be deployed (or mocked via Angular HTTP interceptor).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `DocumentUploadComponent` | MODIFY | Add OCR status polling after upload completes, extraction status badges, retry button, extracted text preview |
| `DocumentUploadComponent` (template) | MODIFY | Add status badges (`app-badge`), OCR spinner, retry button, extracted text expandable panel, `aria-live` region |
| `DocumentUploadComponent` (styles) | MODIFY | Status badge colours (grey/blue/green/red/amber), retry button styling, expandable panel transition |
| `DocumentUploadService` | MODIFY | Add `retryOcr(documentId: string): Observable<void>` method wrapping `POST /api/v1/documents/{id}/retry-ocr` |
| `UploadFileStatus` model | MODIFY | Add `extractedTextPreview: string`, `needsManualReview: boolean` fields |
| `ExtractionStatus` enum | CREATE | TypeScript enum: `Queued`, `Processing`, `Completed`, `Failed` |

---

## Implementation Plan

1. **Create `ExtractionStatus` enum** in `app/shared/models/extraction-status.enum.ts`: values `Queued = 'queued'`, `Processing = 'processing'`, `Completed = 'completed'`, `Failed = 'failed'`.
2. **Extend `UploadFileStatus` model**: Add `extractionStatus: ExtractionStatus`, `extractedTextPreview: string | null`, `needsManualReview: boolean`, `retryCount: number`.
3. **Extend `DocumentUploadService`**: Add `retryOcr(documentId: string): Observable<void>` calling `POST /api/v1/documents/{id}/retry-ocr`. Update `getStatus()` response mapping to include `extractionStatus`, `extractedTextPreview`, `needsManualReview`.
4. **Extend status polling in `DocumentUploadComponent`**: After a file upload completes with `scanResult = 'Clean'`, continue polling `getStatus()` every 3 seconds to track `extractionStatus`. Update the existing `takeWhile` condition to include OCR terminal states (`Completed`, `Failed`). Display OCR-specific status badges alongside the existing scan badges.
5. **Implement OCR status badges**: In the file list, render `app-badge` per document for extraction status using UXR-404 colour semantics: `variant="neutral"` for Queued (grey), `variant="info"` with inline `app-progress[mode="circular"]` for Processing (blue), `variant="success"` for Completed (green check), `variant="error"` for Failed (red). For low-confidence extractions (Edge Case 1), render `variant="warning"` badge "Manual Review Required" (amber).
6. **Implement retry button**: For files with `extractionStatus = 'Failed'` and `retryCount < 3`, display a "Retry OCR" button. On click, call `documentUploadService.retryOcr(documentId)` and resume polling. Disable the button and show loading spinner during the retry request (UXR-501). After 3 retries, display "Moved to Dead-Letter Queue" message with no retry option.
7. **Implement extracted text preview**: For files with `extractionStatus = 'Completed'`, add an expandable panel (`mat-expansion-panel`) below the file row showing truncated extracted text (first 500 characters) with a "View Full Document" link navigating to the document viewer.
8. **Implement screen reader announcements**: Wrap the status badge area in an `aria-live="polite"` region so status transitions ("Processing started", "OCR completed", "OCR failed — retry available") are announced to assistive technology (UXR-203).

---

## Current Project State

```
app/
├── features/
│   ├── documents/                                    ← MODIFY (this task)
│   │   ├── document-upload.component.ts              ← MODIFY (add OCR status tracking)
│   │   ├── document-upload.component.html            ← MODIFY (add status badges, retry, preview)
│   │   ├── document-upload.component.scss            ← MODIFY (add OCR status styles)
│   │   └── document-upload.service.ts                ← MODIFY (add retryOcr method)
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── upload-file-status.model.ts               ← MODIFY (add extraction fields)
│       ├── extraction-status.enum.ts                 ← CREATE
│       └── [existing models...]
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual feature folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `app/features/documents/document-upload.component.ts` | Add OCR status polling after clean scan, retry logic, extracted text preview state |
| MODIFY | `app/features/documents/document-upload.component.html` | Add extraction status badges, retry button, expanded text preview panel, `aria-live` region |
| MODIFY | `app/features/documents/document-upload.component.scss` | Status badge colour variants (grey/blue/green/red/amber), retry button, expansion panel styles |
| MODIFY | `app/features/documents/document-upload.service.ts` | Add `retryOcr()` method, extend `getStatus()` response mapping |
| MODIFY | `app/shared/models/upload-file-status.model.ts` | Add `extractionStatus`, `extractedTextPreview`, `needsManualReview`, `retryCount` |
| CREATE | `app/shared/models/extraction-status.enum.ts` | `ExtractionStatus` enum: Queued, Processing, Completed, Failed |

---

## External References

- RxJS `interval` with `switchMap` for polling: https://rxjs.dev/api/index/function/interval
- Angular Material Expansion Panel: https://material.angular.io/components/expansion/overview
- WAI-ARIA `aria-live` regions: https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA19
- FR-DM-002: System MUST process uploaded documents with OCR and extraction tracking with completion target under 2 minutes
- NFR-003: System MUST complete OCR and document extraction processing within 2 minutes p95 for files up to 10 MB
- UXR-203: Screen reader announcements MUST be provided for dynamic content updates
- UXR-404: Status indicators MUST use consistent colour semantics
- UXR-501: Form submission buttons MUST show loading spinner and disable during network requests
- SCR-011 wireframe: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-011-document-upload.html`

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

- [ ] Unit tests pass for `DocumentUploadComponent` OCR status polling (Queued → Processing → Completed transitions)
- [ ] Unit tests pass for retry logic (retry button triggers POST, polling resumes, button disabled after 3 retries)
- [ ] Unit tests pass for `DocumentUploadService.retryOcr()` (HTTP call mocked)
- [ ] **[UI Task]** Visual comparison against wireframe at 375px, 768px, 1440px
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Status badges display correct colours per UXR-404: grey (Queued), blue (Processing), green (Completed), red (Failed), amber (Manual Review)
- [ ] Screen reader announces status transitions via `aria-live="polite"` (UXR-203)
- [ ] Retry button shows loading spinner and disables during request (UXR-501)
- [ ] Extracted text preview expands for completed documents
- [ ] Low-confidence extraction displays "Manual Review Required" amber badge (Edge Case 1)
- [ ] Concurrent documents track status independently (Edge Case 2)

---

## Implementation Checklist

- [ ] Create `ExtractionStatus` enum with Queued, Processing, Completed, Failed values
- [ ] Extend `UploadFileStatus` model with `extractionStatus`, `extractedTextPreview`, `needsManualReview`, `retryCount`
- [ ] Extend `DocumentUploadService` with `retryOcr()` method and updated `getStatus()` mapping
- [ ] Implement OCR status polling in `DocumentUploadComponent` after clean scan, with `takeWhile` for OCR terminal states
- [ ] Implement extraction status badges with UXR-404 colour semantics and retry button for Failed state (UXR-501)
- [ ] Implement extracted text preview via `mat-expansion-panel` for Completed documents
- [ ] Add `aria-live="polite"` region for screen reader announcements on status transitions (UXR-203)
- [ ] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation
