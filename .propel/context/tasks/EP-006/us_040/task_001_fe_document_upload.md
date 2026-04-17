---
task_id: task_001
user_story: us_040
epic: EP-006
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_040] Document Upload with Malware Scanning
- **Story Location**: [.propel/context/tasks/EP-006/us_040/us_040.md](.propel/context/tasks/EP-006/us_040/us_040.md)
- **Acceptance Criteria**:
  - AC-1: Given I am authenticated, When I select and submit a file for upload, Then the system validates the file type (PDF, JPG, PNG, TIFF) and size (max 10 MB) before accepting the file.
  - AC-2: Given a valid file is submitted, When the malware scan is executed, Then the scan completes before the file is persisted and a clean file is stored in encrypted cloud storage.
  - AC-3: Given the malware scan detects a threat, When the scan result is returned, Then the file is rejected, not persisted, the upload response returns an error message, and the event is logged.
  - AC-4: Given a file type not in the approved list is submitted, When the type validation runs, Then the API returns HTTP 400 with a message listing the accepted file types.
- **Edge Cases**:
  - Edge Case 1: Malware scanner unavailable — upload is queued in a pending scan state; file is not accessible until scan completes; user is notified of the delayed scan via status badge.
  - Edge Case 2: File exceeds 10 MB — HTTP 400 returned immediately; no partial upload; error toast displayed to user.

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
| **UXR Requirements** | UXR-201, UXR-202, UXR-205, UXR-301, UXR-404, UXR-501, UXR-505 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local wireframe file exists at specified path.
>
> **Note**: US_040 references SCR-022 (Compliance Reports, EP-010). The document upload screen is SCR-011 per figma_spec.md, which is the dedicated Document Upload screen under EP-006.

### Screen States (SCR-011)

| State | Description |
|-------|-------------|
| Default | Dashed drop zone with upload icon, supported format labels (PDF, JPG, PNG, TIFF), and browse button |
| Loading | Upload progress bar per file, scanning status indicator, then OCR processing spinner |
| Empty | Drop zone prompt with illustration |
| Error | File rejection toast (wrong format, oversized, virus detected), OCR failure with retry button |
| Validation | Green check per file on successful upload and scan, processing status badges (queued, processing, completed, pending_scan) |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
| Drag and Drop | Angular CDK DragDrop / native HTML5 | 17.x |
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

Implement the `DocumentUploadComponent` as a standalone Angular 17 component for SCR-011. The component renders a centered drag-and-drop upload zone (max-width 720px) using the `app-file-upload` design system component with supported format labels (PDF, JPG, PNG, TIFF) and a browse button fallback. Users can drag files or click to select. On file selection, client-side validation checks file type (by extension and MIME type) and size (max 10 MB) before initiating the upload. Invalid files display an error toast listing accepted types (AC-4) or the size limit (Edge Case 2). Valid files are uploaded via `POST /api/v1/documents/upload` with an `app-progress` linear progress bar per file (UXR-505). After upload, the component polls `GET /api/v1/documents/{id}/status` to display scan and processing status badges: "Scanning" (blue), "Clean" (green check), "Threat Detected" (red, AC-3), "Pending Scan" (amber, Edge Case 1), "Processing" (blue spinner), "Completed" (green). The file list renders below the drop zone with status badges per file. The submit button shows a loading spinner and disables during upload (UXR-501). Error messages are associated with fields via `aria-describedby` (UXR-205). The component is accessible to both Patient and Staff roles.

---

## Dependent Tasks

- **us_040/task_002** — `POST /api/v1/documents/upload` and `GET /api/v1/documents/{id}/status` endpoints must be deployed (or mocked via Angular HTTP interceptor).
- **us_040/task_003** — `clinical_documents` table with `scan_result` enum must exist for the API to function.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `DocumentUploadComponent` | CREATE | Standalone Angular 17 component — drop zone, file list, status badges, progress bars |
| `DocumentUploadComponent` (template) | CREATE | `app-file-upload` drop zone, file list with `app-progress` per file, status badges, error toasts |
| `DocumentUploadComponent` (styles) | CREATE | Centered layout (max-width 720px), drop zone dashed border, progress bar, status badge colours |
| `DocumentUploadService` | CREATE | Angular service wrapping upload POST and status polling GET endpoints |
| `UploadFileStatus` model | CREATE | TypeScript interface: `documentId`, `fileName`, `fileSize`, `uploadProgress`, `scanResult`, `processingStatus` |
| `DocumentUploadResponse` model | CREATE | TypeScript interface: `documentId`, `scanResult`, `message` |
| App routing module | MODIFY | Register `/documents/upload` route accessible to Patient and Staff roles |

---

## Implementation Plan

1. **Create `DocumentUploadService`** in `app/features/documents/document-upload.service.ts`: method `upload(file: File, patientId: string): Observable<HttpEvent<DocumentUploadResponse>>` using `HttpClient.post('/api/v1/documents/upload', formData, { reportProgress: true, observe: 'events' })` to capture upload progress events. Method `getStatus(documentId: string): Observable<UploadFileStatus>` wrapping `HttpClient.get('/api/v1/documents/{id}/status')`.
2. **Implement drag-and-drop zone**: Use `app-file-upload` component with native HTML5 `dragover`, `dragleave`, `drop` events. Display dashed border drop zone with upload icon and format labels. On drop or browse selection, capture `FileList`.
3. **Implement client-side file validation**: For each file in `FileList`, check: (a) file extension is `.pdf`, `.jpg`, `.jpeg`, `.png`, or `.tiff`/`.tif`; (b) MIME type matches `application/pdf`, `image/jpeg`, `image/png`, `image/tiff`; (c) file size does not exceed `10 * 1024 * 1024` bytes. On invalid type, show `app-toast[variant="error"]` listing accepted formats (AC-4). On oversized file, show `app-toast[variant="error"]` "File exceeds the maximum allowed size of 10 MB" (Edge Case 2). Do not proceed with upload for invalid files.
4. **Implement upload with progress**: For each valid file, call `documentUploadService.upload()`. Listen to `HttpEventType.UploadProgress` events to update `app-progress` linear bar per file. On `HttpEventType.Response`, read the `DocumentUploadResponse` to get `documentId` and initial `scanResult`.
5. **Implement status polling**: After upload completes, poll `documentUploadService.getStatus(documentId)` every 3 seconds using `interval(3000).pipe(switchMap(...), takeWhile(status => !isFinal(status)))`. Display scan/processing badges using `app-badge`: "Scanning" (blue), "Clean" (green, `app-badge[variant="success"]`), "Threat Detected" (red, `app-badge[variant="error"]`), "Pending Scan" (amber, `app-badge[variant="warning"]` with "Scanner unavailable — scan queued" message per Edge Case 1). Stop polling when status reaches a terminal state (Clean, ThreatDetected, Completed, Failed).
6. **Implement file list**: Below the drop zone, render a list of uploaded files with columns: file name, file size (formatted), status badge, progress bar (during upload). Use `@for` control flow. Each file row is independently tracked.
7. **Implement error handling**: On malware detection (AC-3), display `app-banner[variant="error"]` "File rejected: malware detected" and remove the file from the list. On upload HTTP error, display retry button per file. On scanner unavailability (Edge Case 1), display `app-banner[variant="warning"]` "Scan pending — file will be available after scan completes".
8. **Register route and FAB**: Add `/documents/upload` route accessible to Patient and Staff. Add `app-fab` (floating action button) for quick upload access per SCR-011 layout.

---

## Current Project State

```
app/
├── features/
│   ├── documents/                                    ← CREATE (this task)
│   │   ├── document-upload.component.ts
│   │   ├── document-upload.component.html
│   │   ├── document-upload.component.scss
│   │   └── document-upload.service.ts
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── upload-file-status.model.ts               ← CREATE
│       ├── document-upload-response.model.ts         ← CREATE
│       └── [existing models...]
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual feature folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/documents/document-upload.component.ts` | Standalone component: drop zone, file validation, upload with progress, status polling |
| CREATE | `app/features/documents/document-upload.component.html` | Template: `app-file-upload` drop zone, file list, `app-progress` bars, status badges, error toasts |
| CREATE | `app/features/documents/document-upload.component.scss` | Centered layout (720px), drop zone styling, status badge colours, responsive breakpoints |
| CREATE | `app/features/documents/document-upload.service.ts` | Service wrapping upload POST (with progress) and status GET endpoints |
| CREATE | `app/shared/models/upload-file-status.model.ts` | `UploadFileStatus` interface |
| CREATE | `app/shared/models/document-upload-response.model.ts` | `DocumentUploadResponse` interface |
| MODIFY | `app/app.routes.ts` | Add `/documents/upload` route for Patient and Staff roles |

---

## External References

- Angular HttpClient progress events: https://angular.dev/guide/http/making-requests#tracking-upload-progress
- HTML5 Drag and Drop API: https://developer.mozilla.org/en-US/docs/Web/API/HTML_Drag_and_Drop_API
- RxJS `interval` with `switchMap` for polling: https://rxjs.dev/api/index/function/interval
- FR-DM-001: System MUST accept PDF, JPG, PNG, and TIFF files up to 10 MB and complete malware scan before persistence
- UXR-201: WCAG 2.1 AA colour contrast ratio of at least 4.5:1
- UXR-202: Full keyboard navigation with visible focus indicators
- UXR-205: Error messages MUST be programmatically associated with form fields using `aria-describedby`
- UXR-301: Mobile (375px), tablet (768px), and desktop (1440px) breakpoints
- UXR-404: Status indicators MUST use consistent colour semantics
- UXR-501: Form submission buttons MUST show loading spinner and disable during network requests
- UXR-505: File upload MUST support drag-and-drop with progress bar and cancel capability
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

- [ ] Unit tests pass for `DocumentUploadComponent` (drop zone render, file validation, upload progress, status polling)
- [ ] Unit tests pass for `DocumentUploadService` (HTTP calls mocked)
- [ ] **[UI Task]** Visual comparison against wireframe at 375px, 768px, 1440px
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Valid files (PDF/JPG/PNG/TIFF <= 10 MB) upload successfully with progress bar
- [ ] Invalid file types show error toast listing accepted formats (AC-4)
- [ ] Oversized files show error toast without initiating upload (Edge Case 2)
- [ ] Malware detection shows red error banner and rejects file (AC-3)
- [ ] Scanner unavailable shows amber warning with "Pending Scan" status (Edge Case 1)
- [ ] Status badges update correctly via polling (Scanning → Clean/ThreatDetected)

---

## Implementation Checklist

- [ ] Create `DocumentUploadService` wrapping upload POST (with `reportProgress`) and status GET polling
- [ ] Implement `app-file-upload` drag-and-drop zone with supported format labels and browse button (UXR-505)
- [ ] Implement client-side file validation: type by extension + MIME, size max 10 MB; show error toasts for failures (AC-1, AC-4)
- [ ] Implement per-file upload with `app-progress` linear progress bar and cancel capability
- [ ] Implement status polling via `interval`/`switchMap` with colour-coded `app-badge` for scan and processing states (UXR-404)
- [ ] Handle malware detection (red error banner, file rejected, AC-3) and scanner unavailability (amber warning, Edge Case 1)
- [ ] Register `/documents/upload` route with Patient and Staff role access; add `app-fab` upload button
- [ ] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation
