## Figma Specification

## Project

**Project Name**: Unified Patient Access and Clinical Intelligence Platform
**Version**: 1.0.0
**Last Updated**: 2025-07-18

## Source References

| Source Document | Path | Key Sections Used |
|----------------|------|-------------------|
| Requirements Specification | `.propel/context/docs/spec.md` | Functional Requirements (FR-UM through FR-AD), Use Cases (UC-001 through UC-006), Actors, NFR |
| Architecture Design | `.propel/context/docs/design.md` | NFR-001 through NFR-012, Technology Stack, AI Architecture |
| UML Models | `.propel/context/docs/models.md` | ERD, Sequence Diagrams, Component Architecture |
| Epics | `.propel/context/docs/epics.md` | EP-001 through EP-011, UI Impact flags |

## UX Requirements

| UXR ID | Category | Requirement | Source | Priority |
|--------|----------|-------------|--------|----------|
| UXR-101 | Usability | Registration flow MUST complete within 30 seconds with minimal form fields and inline validation | FR-UM-001 | High |
| UXR-102 | Usability | Session timeout warning MUST appear as a non-blocking modal 2 minutes before expiry with extend and logout options | FR-UM-004 | High |
| UXR-103 | Usability | Slot search MUST support date-range picker, duration selector, and type filter with results rendering under 1 second | FR-AS-001, NFR-002 | High |
| UXR-104 | Usability | Intake form MUST autosave on field blur and support both AI-assisted and manual entry toggle | FR-AS-002 | High |
| UXR-105 | Usability | Booking confirmation MUST present PDF download, QR code, and ICS export in a single summary view | FR-AS-003 | High |
| UXR-106 | Usability | Queue dashboard MUST display real-time status with color-coded badges and auto-refreshing wait-time estimates | FR-SO-001 | High |
| UXR-107 | Usability | 360-degree patient profile MUST load in under 3 seconds with source traceability links on every data point | FR-CA-002, NFR-002 | High |
| UXR-108 | Usability | Coding suggestion cards MUST show confidence score, rationale text, and accept/modify/reject action buttons in a single view | FR-MC-001, FR-MC-003 | High |
| UXR-109 | Usability | Document viewer MUST support zoom, rotate, and full-text search with keyboard shortcuts | FR-DM-003 | Medium |
| UXR-110 | Usability | Drag-and-drop daily schedule MUST provide visual feedback on drag start, hover target, and drop confirmation | FR-SO-006 | Medium |
| UXR-111 | Usability | All destructive actions (cancel appointment, soft-delete document, reject code) MUST require confirmation dialog | FR-AS-004, FR-DM-004, FR-MC-003 | High |
| UXR-112 | Usability | Waitlist claim countdown MUST display a visible timer with urgency color shift at 30 minutes remaining | FR-AS-005, FR-RN-004 | Medium |
| UXR-201 | Accessibility | All interactive elements MUST meet WCAG 2.1 AA color contrast ratio of at least 4.5:1 for normal text and 3:1 for large text | NFR-009 | High |
| UXR-202 | Accessibility | All forms MUST support full keyboard navigation with visible focus indicators on every interactive element | NFR-009 | High |
| UXR-203 | Accessibility | Screen reader announcements MUST be provided for dynamic content updates including queue changes, alert banners, and toast notifications | NFR-009 | High |
| UXR-204 | Accessibility | All images and icons MUST have alt text or aria-label attributes describing their purpose | NFR-009 | Medium |
| UXR-205 | Accessibility | Error messages MUST be programmatically associated with their form fields using aria-describedby | NFR-009 | High |
| UXR-206 | Accessibility | Focus MUST be trapped within modal dialogs and returned to the trigger element on close | NFR-009 | High |
| UXR-301 | Responsiveness | Application MUST support mobile (375px), tablet (768px), and desktop (1440px) breakpoints with fluid layout between them | NFR-U-001 | High |
| UXR-302 | Responsiveness | Navigation MUST collapse to a hamburger menu on mobile with slide-out drawer pattern | NFR-U-001 | High |
| UXR-303 | Responsiveness | Data tables MUST switch to card-based layout on screens below 768px | NFR-U-001 | Medium |
| UXR-304 | Responsiveness | Touch targets on mobile MUST be at least 44x44 pixels | NFR-U-001 | High |
| UXR-401 | Visual Design | Platform MUST use a healthcare-appropriate color palette with calming blue primary, neutral grays, and clinical accent colors | Design Decision | High |
| UXR-402 | Visual Design | Typography hierarchy MUST use a maximum of 2 font families with consistent size scale (12, 14, 16, 20, 24, 32px) | Design Decision | High |
| UXR-403 | Visual Design | Cards and containers MUST use consistent border-radius (8px) and elevation shadows for depth hierarchy | Design Decision | Medium |
| UXR-404 | Visual Design | Status indicators MUST use consistent color semantics: green for success, amber for warning, red for error, blue for info | Design Decision | High |
| UXR-405 | Visual Design | AI-generated content MUST be visually distinguished from user-entered data using a labeled badge or distinct background tint | AIR-003, AIR-004 | High |
| UXR-501 | Interaction | Form submission buttons MUST show loading spinner and disable during network requests to prevent double submission | NFR-002 | High |
| UXR-502 | Interaction | Toast notifications MUST auto-dismiss after 5 seconds for success and persist until dismissed for errors | Design Decision | Medium |
| UXR-503 | Interaction | Appointment slot selection MUST highlight selected slot with border emphasis and show selection summary in a sticky footer | FR-AS-001 | Medium |
| UXR-504 | Interaction | Conflict alerts MUST use a dismissible but persistent banner pattern with severity-colored left border | FR-CA-003 | High |
| UXR-505 | Interaction | File upload MUST support drag-and-drop with progress bar and cancel capability | FR-DM-001 | Medium |
| UXR-506 | Interaction | Code search autocomplete MUST display results within 300ms of keystroke with keyboard-navigable dropdown | FR-MC-004 | Medium |
| UXR-601 | Error Handling | All form validation errors MUST appear inline below the field with red text and error icon | Design Decision | High |
| UXR-602 | Error Handling | Network failure MUST display a retry banner at the top of the affected view with a retry action button | Design Decision | High |
| UXR-603 | Error Handling | Empty states MUST display an illustration, descriptive message, and primary action CTA | Design Decision | Medium |
| UXR-604 | Error Handling | 404 and forbidden states MUST display branded error pages with navigation back to the dashboard | Design Decision | Medium |
| UXR-605 | Error Handling | OCR processing failure MUST display failed status with a retry button and option to manually enter data | FR-DM-002 | Medium |
| UXR-606 | Error Handling | Account lockout MUST display remaining lockout time with countdown and support contact information | FR-UM-005 | Medium |

### UXR Expansion Details

**UXR-101**: The registration page uses a single-column centered layout with
progressive disclosure. Step 1 captures email or phone. Step 2 captures
verification code. Step 3 captures name and password. Each step auto-advances
on successful validation. The 30-second target applies to the happy path with
pre-filled browser autofill support.

**UXR-106**: The queue dashboard uses a data table with colored status badges
in the leftmost column. Rows auto-reorder when status changes. Wait-time
estimates update via polling every 15 seconds. Staff can click any row to
expand patient details inline without navigation.

**UXR-107**: The 360-degree profile uses a tabbed layout with Summary,
Timeline, Documents, Insurance, and Coding tabs. Every extracted data item
shows a source link icon that opens the originating document in a side panel.
Loading skeleton appears for each tab independently.

**UXR-108**: Each coding suggestion renders as a card with code badge,
confidence percentage bar, rationale text block, and three action buttons
(Accept, Modify, Reject) anchored at the card bottom. Cards stack vertically
with clear visual grouping per ICD-10 and CPT sections.

## Personas Summary

| Persona | Role | Key Goals | Primary Screens | Technical Proficiency |
|---------|------|-----------|-----------------|----------------------|
| Patient | External end user | Book appointments, receive reminders, upload documents, manage insurance | SCR-001, SCR-002, SCR-004, SCR-005, SCR-006, SCR-007, SCR-009, SCR-011 | Low to Medium |
| Staff | Front desk / operations | Manage queue, check-in arrivals, handle walk-ins, override scheduling, assist bookings | SCR-025, SCR-026, SCR-027, SCR-028, SCR-029, SCR-004, SCR-007 | Medium |
| Clinician | Clinical user | Validate extracted data, review conflicts, approve coding decisions | SCR-014, SCR-015, SCR-016, SCR-017, SCR-018, SCR-013 | Medium to High |
| Admin | Governance user | Configure policies, manage users, review audits, generate reports | SCR-019, SCR-020, SCR-021, SCR-022, SCR-023, SCR-024 | Medium to High |

## Information Architecture

### Navigation Structure

```
App Shell (SCR-030)
├── Patient Portal
│   ├── Dashboard (landing)
│   ├── Appointments
│   │   ├── Search & Book (SCR-004 → SCR-005 → SCR-006)
│   │   ├── My Appointments (SCR-007)
│   │   └── Waitlist (SCR-008)
│   ├── Documents
│   │   ├── Upload (SCR-011)
│   │   └── My Documents (SCR-012 → SCR-013)
│   ├── Insurance (SCR-028)
│   └── Settings
│       └── Notification Preferences (SCR-009)
├── Staff Portal
│   ├── Queue Dashboard (SCR-025)
│   ├── Daily Schedule (SCR-026)
│   ├── Book for Patient (SCR-027)
│   ├── Walk-in Registration (SCR-029)
│   ├── Patient Lookup
│   │   ├── 360° Profile (SCR-014)
│   │   ├── Timeline (SCR-015)
│   │   └── Documents (SCR-012 → SCR-013)
│   └── Insurance Verification (SCR-028)
├── Clinician Portal
│   ├── Patient Queue
│   ├── Patient Profile (SCR-014)
│   ├── Clinical Timeline (SCR-015)
│   ├── Conflict Alerts (SCR-016)
│   ├── Coding Review (SCR-017)
│   └── Code Search (SCR-018)
└── Admin Portal
    ├── KPI Dashboard (SCR-023)
    ├── User Management (SCR-020)
    ├── System Configuration (SCR-019)
    ├── Template Editor (SCR-024)
    ├── Audit Logs (SCR-021)
    └── Compliance Reports (SCR-022)
```

### Role-Based Access Matrix

| Screen | Patient | Staff | Clinician | Admin |
|--------|---------|-------|-----------|-------|
| SCR-001 Registration | Write | — | — | — |
| SCR-002 Login | Read/Write | Read/Write | Read/Write | Read/Write |
| SCR-003 Password Reset | Read/Write | Read/Write | Read/Write | Read/Write |
| SCR-004 Slot Search | Read/Write | Read/Write | — | — |
| SCR-005 Intake Form | Read/Write | Read/Write | — | — |
| SCR-006 Booking Confirmation | Read | Read | — | — |
| SCR-007 Appointment History | Read | Read | — | — |
| SCR-008 Waitlist | Read | Read/Write | — | — |
| SCR-009 Notification Preferences | Read/Write | — | — | — |
| SCR-011 Document Upload | Write | Write | — | — |
| SCR-012 Document Library | Read | Read | Read | — |
| SCR-013 Document Viewer | Read | Read | Read | — |
| SCR-014 360° Patient Profile | — | Read | Read/Write | — |
| SCR-015 Clinical Timeline | — | Read | Read | — |
| SCR-016 Conflict Alerts | — | Read | Read/Write | — |
| SCR-017 Coding Review | — | — | Read/Write | — |
| SCR-018 Code Search | — | — | Read/Write | — |
| SCR-019 System Configuration | — | — | — | Read/Write |
| SCR-020 User Management | — | — | — | Read/Write |
| SCR-021 Audit Logs | — | — | — | Read |
| SCR-022 Compliance Reports | — | — | — | Read |
| SCR-023 KPI Dashboard | — | — | — | Read |
| SCR-024 Template Editor | — | — | — | Read/Write |
| SCR-025 Queue Dashboard | — | Read/Write | — | — |
| SCR-026 Daily Schedule | — | Read/Write | — | — |
| SCR-027 Staff Booking | — | Read/Write | — | — |
| SCR-028 Insurance Verification | Read/Write | Read/Write | — | — |
| SCR-029 Walk-in Registration | — | Read/Write | — | — |
| SCR-030 App Shell | Read | Read | Read | Read |

## Screen Inventory

### SCR-001: Patient Registration

**Epic**: EP-001
**Use Case**: UC-001
**Personas**: Patient
**UXR References**: UXR-101, UXR-201, UXR-202, UXR-205, UXR-301, UXR-601
**Description**: Three-step progressive registration flow with email/phone
verification, code entry, and profile completion.

**States**:

| State | Description |
|-------|-------------|
| Default | Step 1 displayed with email/phone input field, registration CTA, and login link |
| Loading | Spinner on CTA button during verification code send and account creation |
| Empty | N/A — registration always shows input fields |
| Error | Inline validation errors for invalid email format, expired code, duplicate account, and failed verification |
| Validation | Green checkmarks on completed steps, real-time format validation on each field |

**Layout**: Single-column centered (max-width 480px), branded header with logo,
step indicator at top, form below, footer with terms and privacy links.

---

### SCR-002: Login

**Epic**: EP-001
**Use Case**: UC-001
**Personas**: Patient, Staff, Clinician, Admin
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-601, UXR-606
**Description**: Unified login page with email/password fields, remember-me
option, and role-appropriate redirect.

**States**:

| State | Description |
|-------|-------------|
| Default | Email and password fields, login CTA, forgot password link, register link |
| Loading | Spinner on login button during authentication |
| Empty | Fields empty with placeholder text hints |
| Error | Inline field errors for invalid credentials, account lockout banner with countdown timer |
| Validation | Real-time email format check, password field shows/hide toggle |

**Layout**: Single-column centered (max-width 480px), branded header, form
center-aligned, social login placeholder (future).

---

### SCR-003: Password Reset

**Epic**: EP-001
**Use Case**: UC-001
**Personas**: Patient, Staff, Clinician, Admin
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-601
**Description**: Two-step password reset: enter email, then enter code and new
password.

**States**:

| State | Description |
|-------|-------------|
| Default | Email input with send reset code CTA |
| Loading | Spinner during code send and password update |
| Empty | N/A — always shows input fields |
| Error | Inline errors for unrecognized email, expired code, weak password |
| Validation | Success confirmation with redirect to login after reset |

**Layout**: Single-column centered (max-width 480px), step indicator, branded
header.

---

### SCR-004: Slot Search and Discovery

**Epic**: EP-002
**Use Case**: UC-002
**Personas**: Patient, Staff
**UXR References**: UXR-103, UXR-201, UXR-202, UXR-301, UXR-303, UXR-503
**Description**: Search available appointment slots by date range, duration,
and type. Results display as a time-grid or list with selectable slots.

**States**:

| State | Description |
|-------|-------------|
| Default | Filter bar (date picker, duration dropdown, type selector) with search button, results area below |
| Loading | Skeleton cards in results area during slot fetch |
| Empty | Illustration with "No available slots" message and waitlist join CTA |
| Error | Retry banner for network failure, inline error for invalid date range |
| Validation | Selected slot highlighted with border emphasis, sticky footer showing selection summary and "Continue to Intake" CTA |

**Layout**: Responsive — desktop uses side filter panel with grid results;
mobile uses stacked filters with vertical slot cards.

---

### SCR-005: Intake Form

**Epic**: EP-002
**Use Case**: UC-002
**Personas**: Patient, Staff
**UXR References**: UXR-104, UXR-201, UXR-202, UXR-205, UXR-301, UXR-501,
UXR-601
**Description**: Multi-section intake form with autosave, supporting AI-assisted
prefill toggle and manual entry. Sections: personal info, reason for visit,
medical history, insurance reference.

**States**:

| State | Description |
|-------|-------------|
| Default | Form sections displayed with AI-assist toggle in header, autosave indicator in footer |
| Loading | Skeleton fields while loading existing draft; spinner on submit button |
| Empty | Blank form with placeholder guidance text per section |
| Error | Inline field errors, section-level error summary, autosave failure warning toast |
| Validation | Section completion checkmarks, green progress bar, AI-filled fields distinguished with badge (UXR-405) |

**Layout**: Single-column form layout (max-width 720px), sticky bottom bar
with Back and Submit buttons, autosave status indicator.

---

### SCR-006: Booking Confirmation

**Epic**: EP-002
**Use Case**: UC-002
**Personas**: Patient, Staff
**UXR References**: UXR-105, UXR-201, UXR-301
**Description**: Post-booking summary showing appointment details, PDF download,
QR code, and ICS calendar export.

**States**:

| State | Description |
|-------|-------------|
| Default | Confirmation card with appointment details, action buttons for PDF, QR, and ICS |
| Loading | Spinner while generating confirmation artifacts |
| Empty | N/A — always shows confirmation data |
| Error | Retry button if artifact generation fails |
| Validation | Green success banner with check icon at top |

**Layout**: Single-column centered card (max-width 600px) with appointment
summary, action buttons row, and navigation to appointment list.

---

### SCR-007: Appointment History

**Epic**: EP-002
**Use Case**: UC-002
**Personas**: Patient, Staff
**UXR References**: UXR-201, UXR-301, UXR-303, UXR-111
**Description**: Paginated list of past and upcoming appointments with date
and status filters, PDF export, and reschedule/cancel actions.

**States**:

| State | Description |
|-------|-------------|
| Default | Filter bar (date range, status dropdown), appointment table with action column |
| Loading | Skeleton table rows during data fetch |
| Empty | Illustration with "No appointments found" and book CTA |
| Error | Retry banner for data load failure |
| Validation | Confirmation dialog for cancel action, success toast after reschedule |

**Layout**: Full-width table on desktop, card list on mobile. Filter bar above,
pagination below.

---

### SCR-008: Waitlist View

**Epic**: EP-002
**Use Case**: UC-002, UC-003
**Personas**: Patient, Staff
**UXR References**: UXR-112, UXR-201, UXR-301
**Description**: View active waitlist entries with preferred slot criteria and
claim countdown timers for offered slots.

**States**:

| State | Description |
|-------|-------------|
| Default | List of waitlist entries with slot preference, status, and countdown timer for offered slots |
| Loading | Skeleton cards during data fetch |
| Empty | Illustration with "Not on any waitlist" and browse slots CTA |
| Error | Retry banner on network failure |
| Validation | Claim button with urgency color shift at 30 minutes remaining; confirmation dialog on claim |

**Layout**: Card-based list, countdown timer with color-coded urgency
(green > 1h, amber 30m–1h, red < 30m).

---

### SCR-009: Notification Preferences

**Epic**: EP-003
**Use Case**: UC-003
**Personas**: Patient
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-501
**Description**: Patient-configurable notification channel (email, SMS) and
reminder timing preferences.

**States**:

| State | Description |
|-------|-------------|
| Default | Toggle switches for email and SMS channels, checkbox list for reminder timings (7d, 2d, 1d, 2h) |
| Loading | Spinner on Save button during preference update |
| Empty | Default preferences pre-selected (all channels, all timings) |
| Error | Toast notification for save failure with retry |
| Validation | Success toast on save, current selections visually confirmed |

**Layout**: Single-column settings card (max-width 600px).

---

### SCR-011: Document Upload

**Epic**: EP-006
**Use Case**: UC-004
**Personas**: Patient, Staff
**UXR References**: UXR-505, UXR-201, UXR-301, UXR-605
**Description**: Drag-and-drop file upload zone accepting PDF, JPG, PNG, TIFF
up to 10 MB with malware scan status and OCR processing progress.

**States**:

| State | Description |
|-------|-------------|
| Default | Dashed drop zone with upload icon, supported format labels, and browse button |
| Loading | Upload progress bar per file, scanning status indicator, then OCR processing spinner |
| Empty | Drop zone prompt with illustration |
| Error | File rejection toast (wrong format, oversized, virus), OCR failure with retry button |
| Validation | Green check per file on successful upload and scan, processing status badges (queued, processing, completed) |

**Layout**: Centered drop zone (max-width 720px), file list below with status
badges.

---

### SCR-012: Document Library

**Epic**: EP-006
**Use Case**: UC-004
**Personas**: Patient, Staff, Clinician
**UXR References**: UXR-201, UXR-301, UXR-303, UXR-111
**Description**: Filterable list of patient documents with category tags,
processing status, and actions (view, rename, delete).

**States**:

| State | Description |
|-------|-------------|
| Default | Filter bar (category, date, status), document table with columns: name, category, date, status, actions |
| Loading | Skeleton rows during fetch |
| Empty | Illustration with "No documents" and upload CTA |
| Error | Retry banner on load failure |
| Validation | Confirmation dialog for soft-delete, success toast for rename |

**Layout**: Full-width table on desktop, card layout on mobile.

---

### SCR-013: Document Viewer

**Epic**: EP-006
**Use Case**: UC-004
**Personas**: Patient, Staff, Clinician
**UXR References**: UXR-109, UXR-201, UXR-202
**Description**: In-browser document rendering with zoom, rotate, full-text
search, and extracted entity sidebar.

**States**:

| State | Description |
|-------|-------------|
| Default | Document rendered in main panel, toolbar with zoom/rotate/search controls, extracted entities sidebar |
| Loading | Skeleton document placeholder during file load |
| Empty | N/A — viewer always opens with a selected document |
| Error | "Failed to load document" message with retry button |
| Validation | Search results highlighted in document, extracted entities listed with confidence badges |

**Layout**: Split view — document viewer (70%) with entity sidebar (30%).
Toolbar pinned at top. Mobile: sidebar collapses to bottom sheet.

---

### SCR-014: 360-Degree Patient Profile

**Epic**: EP-007
**Use Case**: UC-005
**Personas**: Staff, Clinician
**UXR References**: UXR-107, UXR-201, UXR-202, UXR-301, UXR-405
**Description**: Tabbed patient profile with Summary, Timeline, Documents,
Insurance, and Coding tabs. Each data point shows source traceability link.

**States**:

| State | Description |
|-------|-------------|
| Default | Patient header (name, DOB, MRN), tab bar, active tab content with source icons |
| Loading | Independent skeleton loaders per tab content area; profile loads under 3 seconds |
| Empty | Per-tab empty states with contextual messages and action CTAs |
| Error | Per-tab error banner with retry for individual data source failures |
| Validation | AI-extracted data marked with purple "AI" badge; verified data marked with green checkmark |

**Layout**: Full-width patient header, horizontal tab bar, content area below.
Source link icons open document viewer in side panel. Mobile: tabs become
scrollable horizontal strip.

---

### SCR-015: Clinical Timeline

**Epic**: EP-007
**Use Case**: UC-005
**Personas**: Staff, Clinician
**UXR References**: UXR-201, UXR-301
**Description**: Chronological timeline of clinical events (medications,
diagnoses, allergies, visits) with category filters and print support.

**States**:

| State | Description |
|-------|-------------|
| Default | Vertical timeline with event cards, filter chips for category and date range, print button |
| Loading | Skeleton timeline entries during data fetch |
| Empty | "No clinical events recorded" with document upload CTA |
| Error | Retry banner on load failure |
| Validation | Filter applied indicator, print preview modal |

**Layout**: Single-column timeline with left date markers and right event
cards. Filter bar pinned above. Print-friendly stylesheet applied on print.

---

### SCR-016: Conflict Alerts

**Epic**: EP-007
**Use Case**: UC-005
**Personas**: Staff, Clinician
**UXR References**: UXR-504, UXR-201, UXR-202, UXR-111
**Description**: Drug-drug and drug-allergy conflict alerts with severity
classification and mandatory clinician acknowledgment for critical alerts.

**States**:

| State | Description |
|-------|-------------|
| Default | Alert cards sorted by severity (critical first), each with severity badge, description, conflicting items, and acknowledge button |
| Loading | Skeleton alert cards during conflict evaluation |
| Empty | "No conflicts detected" success message |
| Error | Retry banner if conflict detection fails |
| Validation | Critical alerts require typed confirmation; acknowledged alerts move to resolved section |

**Layout**: Single-column alert cards with severity-colored left border
(red: critical, orange: high, yellow: moderate, blue: low). Acknowledge
button on each card; critical alerts require confirmation dialog.

---

### SCR-017: Coding Suggestion Review

**Epic**: EP-008
**Use Case**: UC-005
**Personas**: Clinician
**UXR References**: UXR-108, UXR-201, UXR-202, UXR-405, UXR-501, UXR-111
**Description**: AI-generated ICD-10 and CPT coding suggestions with
confidence scores, explainable rationale, and accept/modify/reject workflow.

**States**:

| State | Description |
|-------|-------------|
| Default | ICD-10 section with top-3 suggestion cards, CPT section below, each card shows code, confidence bar, rationale, action buttons |
| Loading | Skeleton suggestion cards during AI inference (max 2.5s) |
| Empty | "No suggestions available — manual coding required" with code search link |
| Error | AI service failure banner with fallback to manual coding, retry option |
| Validation | Accepted codes shown with green border, modified codes editable inline, rejected codes grayed with strikethrough |

**Layout**: Two-section layout (ICD-10 and CPT), suggestion cards stacked
vertically within each section. Action buttons at card bottom. Summary bar
at top showing finalization status.

---

### SCR-018: Code Search

**Epic**: EP-008
**Use Case**: UC-005
**Personas**: Clinician
**UXR References**: UXR-506, UXR-201, UXR-202
**Description**: Searchable code lookup with autocomplete for ICD-10 and CPT
codes, keyword search, and favorites management.

**States**:

| State | Description |
|-------|-------------|
| Default | Search input with autocomplete dropdown, favorites section below, recent searches |
| Loading | Autocomplete dropdown shows loading indicator during search |
| Empty | "Start typing to search codes" prompt with popular codes suggestion |
| Error | "Search unavailable" inline message with retry |
| Validation | Selected code highlighted, results show code, description, and add-to-favorites star |

**Layout**: Single-column with search input at top, autocomplete dropdown
overlay, results list below, favorites section in sidebar (desktop) or
collapsible section (mobile).

---

### SCR-019: System Configuration

**Epic**: EP-011
**Use Case**: UC-006
**Personas**: Admin
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-501
**Description**: Admin settings for slot templates, reminder rules, session
policies, and communication template configuration.

**States**:

| State | Description |
|-------|-------------|
| Default | Tabbed or accordion sections for each configuration category, current values displayed with edit buttons |
| Loading | Skeleton content per section during config load |
| Empty | Default system values shown with "customize" prompts |
| Error | Validation errors inline, save failure toast with retry |
| Validation | Save confirmation toast, version history accessible per config section |

**Layout**: Sidebar navigation for config categories (desktop), accordion
on mobile. Form content area with Save and Reset buttons.

---

### SCR-020: User Management

**Epic**: EP-011
**Use Case**: UC-006
**Personas**: Admin
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-303, UXR-111
**Description**: User lifecycle administration with list, invite, activate,
deactivate, bulk actions, and activity history.

**States**:

| State | Description |
|-------|-------------|
| Default | User table with columns: name, email, role, status, last active, actions. Bulk action toolbar. Invite button. |
| Loading | Skeleton table during user list fetch |
| Empty | "No users found" with invite CTA |
| Error | Retry banner on load failure, error toast for failed bulk action |
| Validation | Confirmation dialog for deactivation and bulk actions, success toast for invite sent |

**Layout**: Full-width data table with toolbar above for search, filter, and
bulk actions. User detail side panel on row click.

---

### SCR-021: Audit Log Viewer

**Epic**: EP-010
**Use Case**: UC-006
**Personas**: Admin
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-303
**Description**: Read-only audit trail viewer with filters for event type,
user, date range, and entity. Restricted to admin access.

**States**:

| State | Description |
|-------|-------------|
| Default | Filter bar (event type, user, date range), audit event table with pagination |
| Loading | Skeleton rows during fetch, progress indicator for large date ranges |
| Empty | "No audit events match filters" with clear-filters CTA |
| Error | Retry banner on load failure |
| Validation | Applied filters shown as removable chips above table |

**Layout**: Full-width data table with filter bar above and pagination below.
Row click expands event detail inline.

---

### SCR-022: Compliance Reports

**Epic**: EP-010
**Use Case**: UC-006
**Personas**: Admin
**UXR References**: UXR-201, UXR-301
**Description**: HIPAA compliance report generation with scheduling,
preview, and export (PDF, email distribution).

**States**:

| State | Description |
|-------|-------------|
| Default | Report type selector, date range picker, generate button, list of previously generated reports |
| Loading | Progress bar during report generation |
| Empty | "No reports generated yet" with generate CTA |
| Error | Generation failure alert with retry |
| Validation | Generated report available for preview and download |

**Layout**: Single-column with report configuration at top, report list below
with download and preview actions per row.

---

### SCR-023: KPI Dashboard

**Epic**: EP-011
**Use Case**: UC-006
**Personas**: Admin
**UXR References**: UXR-201, UXR-301
**Description**: Operational KPI dashboard with charts for no-show rate,
appointment utilization, wait times, and staff productivity. Supports chart
export and scheduled email distribution.

**States**:

| State | Description |
|-------|-------------|
| Default | Grid of KPI cards and charts with date range selector, export button, schedule distribution button |
| Loading | Skeleton chart placeholders during data load |
| Empty | "Insufficient data for KPI calculation" with date range suggestion |
| Error | Per-widget error state with retry, partial dashboard render on partial failure |
| Validation | Date range applied indicator, export success toast |

**Layout**: Dashboard grid — 2 to 4 columns of chart cards on desktop,
single column on mobile. Date range selector pinned at top.

---

### SCR-024: Template Editor

**Epic**: EP-011
**Use Case**: UC-006
**Personas**: Admin
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-501
**Description**: HTML and SMS notification template editor with live preview,
version history, and rollback support.

**States**:

| State | Description |
|-------|-------------|
| Default | Split view: code editor left, live preview right. Template selector dropdown, version history panel. |
| Loading | Skeleton editor and preview during template load |
| Empty | Starter template with placeholder content |
| Error | Save failure toast with retry, invalid template validation errors inline |
| Validation | Version saved toast, preview updates on edit, rollback confirmation dialog |

**Layout**: Split view on desktop (50/50 editor/preview). Mobile: tabbed
switch between editor and preview. Version history in collapsible sidebar.

---

### SCR-025: Queue Dashboard

**Epic**: EP-004
**Use Case**: UC-002, UC-003
**Personas**: Staff
**UXR References**: UXR-106, UXR-201, UXR-202, UXR-301, UXR-303
**Description**: Real-time patient queue with color-coded status, wait-time
estimates, one-click check-in, and inline patient detail expansion.

**States**:

| State | Description |
|-------|-------------|
| Default | Queue table with status badges (color-coded), wait time column, patient name, appointment type, action buttons |
| Loading | Skeleton rows during initial load, shimmer on auto-refresh |
| Empty | "No patients in queue" with walk-in CTA |
| Error | Connection lost banner with auto-reconnect indicator |
| Validation | Check-in confirmation toast, status transition animation |

**Layout**: Full-width data table with status color column. Auto-refreshes
every 15 seconds. Row expansion shows patient details and quick actions.

---

### SCR-026: Daily Schedule View

**Epic**: EP-004
**Use Case**: UC-002
**Personas**: Staff
**UXR References**: UXR-110, UXR-201, UXR-202, UXR-301
**Description**: Day view calendar with appointment blocks, drag-and-drop
rearrangement, and print-friendly rendering.

**States**:

| State | Description |
|-------|-------------|
| Default | Time-grid calendar (7AM–7PM) with appointment blocks, date picker, print button |
| Loading | Skeleton time-grid during data fetch |
| Empty | "No appointments scheduled" for selected date |
| Error | Load failure banner with retry |
| Validation | Drag feedback (ghost block, valid/invalid drop zone), drop confirmation toast, print preview modal |

**Layout**: Time-grid with 15-minute intervals on desktop. Appointment blocks
color-coded by type. Print button triggers print-optimized layout.

---

### SCR-027: Staff-Assisted Booking

**Epic**: EP-004
**Use Case**: UC-002
**Personas**: Staff
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-501
**Description**: Streamlined booking flow for staff creating appointments on
behalf of patients, without patient-side verification.

**States**:

| State | Description |
|-------|-------------|
| Default | Patient search/select, slot picker, simplified intake, override reason field (if policy override needed) |
| Loading | Spinner during patient search and booking creation |
| Empty | Patient search prompt |
| Error | Booking failure with reason, override validation error |
| Validation | Override reason required if scheduling constraint bypassed, booking confirmation toast |

**Layout**: Multi-step wizard: Step 1 Patient Select, Step 2 Slot Pick,
Step 3 Intake/Override, Step 4 Confirm.

---

### SCR-028: Insurance Verification

**Epic**: EP-005
**Use Case**: UC-002
**Personas**: Patient, Staff
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-505
**Description**: Insurance detail entry and card image upload with soft
validation, primary/secondary insurance support, and verification reports.

**States**:

| State | Description |
|-------|-------------|
| Default | Form for primary insurance (policy number, provider, group), card image upload zones (front/back), secondary insurance toggle |
| Loading | Spinner during validation check and image upload |
| Empty | "No insurance on file" with add CTA |
| Error | Soft validation warnings (non-blocking), upload failure with retry |
| Validation | Format validation results shown inline (pass/warn), verification status badge |

**Layout**: Single-column form with card image upload zones stacked.
Verification report accessible via link below form.

---

### SCR-029: Walk-in Registration

**Epic**: EP-004
**Use Case**: UC-002
**Personas**: Staff
**UXR References**: UXR-201, UXR-202, UXR-301, UXR-501
**Description**: Quick-entry form for walk-in patients with queue insertion
and optional conversion to registered patient.

**States**:

| State | Description |
|-------|-------------|
| Default | Minimal form: name, phone, reason. Add-to-queue CTA. Convert-to-patient toggle. |
| Loading | Spinner on queue insertion |
| Empty | N/A — always shows input fields |
| Error | Validation errors inline, queue insertion failure toast |
| Validation | Success toast with queue position number, patient conversion confirmation |

**Layout**: Compact single-column form (max-width 480px) optimized for fast
data entry.

---

### SCR-030: Application Shell

**Epic**: EP-TECH
**Use Case**: All
**Personas**: Patient, Staff, Clinician, Admin
**UXR References**: UXR-302, UXR-201, UXR-202, UXR-301, UXR-304
**Description**: Top-level navigation shell with role-based sidebar, header
with user menu, breadcrumbs, and session timeout warning modal.

**States**:

| State | Description |
|-------|-------------|
| Default | Sidebar navigation (role-filtered), header with logo, user avatar dropdown, breadcrumbs, main content area |
| Loading | Global loading bar at top of viewport |
| Empty | N/A — shell always renders |
| Error | Global error boundary with friendly error page and navigation back |
| Validation | Session timeout warning modal with extend/logout buttons (UXR-102) |

**Layout**: Sidebar (240px collapsed 64px) + header (56px) + content area.
Mobile: hamburger menu with slide-out drawer. Breadcrumbs below header.

## Content and Tone

**Voice**: Professional, reassuring, and clear. Medical terminology is used
accurately but supplemented with plain-language descriptions where patients
interact.

**Tone Matrix**:

| Context | Tone | Example |
|---------|------|---------|
| Success states | Reassuring, concise | "Your appointment is confirmed for June 15 at 10:00 AM." |
| Error states | Empathetic, actionable | "We couldn't complete your booking. Please try again or call our office." |
| Warnings | Clear, non-alarming | "Your session will expire in 2 minutes. Would you like to continue?" |
| AI outputs | Transparent, factual | "Suggested code: M54.5 — Low back pain (Confidence: 92%). Based on clinical notes from visit 2025-06-01." |
| Clinical alerts | Direct, urgency-appropriate | "Critical: Drug interaction detected between Warfarin and Aspirin. Clinician acknowledgment required." |
| Empty states | Encouraging, action-oriented | "No documents uploaded yet. Upload your first clinical document to get started." |

## Data and Edge Cases

### Key Data Scenarios

| Scenario | Screen(s) | Handling |
|----------|-----------|----------|
| Patient with 500+ documents | SCR-012, SCR-014 | Paginated with virtual scroll, lazy loading per page |
| Concurrent slot booking race | SCR-004, SCR-006 | Optimistic lock with conflict error and auto-refresh of available slots |
| OCR processing timeout (>2 min) | SCR-011, SCR-012 | Status badge shows "Processing Failed", retry button, manual entry fallback link |
| Zero AI confidence for coding | SCR-017 | Empty suggestion state with "Manual coding required" and code search link |
| Multiple critical conflict alerts | SCR-016 | Sorted by severity descending, critical alerts render first with red emphasis |
| 2-hour waitlist claim window expiry | SCR-008 | Slot auto-released, patient notified, entry moved to expired section |
| Account locked after 5 attempts | SCR-002 | Lockout banner with 30-minute countdown timer and support contact link |
| Large date range audit query | SCR-021 | Progressive loading with progress indicator, paginated results |
| Stale queue data (connection drop) | SCR-025 | "Connection lost" banner, auto-reconnect every 10s, stale data dimmed |

### Character Limits

| Field | Max Characters | Screen(s) |
|-------|---------------|-----------|
| Patient name | 100 | SCR-001, SCR-014, SCR-025 |
| Email | 254 | SCR-001, SCR-002 |
| Appointment reason | 500 | SCR-005, SCR-027 |
| Override reason | 300 | SCR-027 |
| Code search query | 100 | SCR-018 |
| Template content (HTML) | 10000 | SCR-024 |
| Template content (SMS) | 160 | SCR-024 |
| Document file name | 255 | SCR-011, SCR-012 |

## Branding and Visual Direction

**Brand Personality**: Trustworthy, modern, clinical, accessible.

**Color Direction**:
- Primary: Calming healthcare blue (#1976D2)
- Secondary: Warm accent for CTAs (#26A69A teal)
- Neutral: Gray scale for backgrounds and text (#FAFAFA, #E0E0E0, #616161, #212121)
- Semantic: Success green (#4CAF50), Warning amber (#FF9800), Error red (#F44336), Info blue (#2196F3)
- AI Accent: Purple tint (#7E57C2) for AI-generated content distinction

**Typography Direction**:
- Primary font: Inter (sans-serif) for UI
- Monospace: JetBrains Mono for code display (SCR-017, SCR-018, SCR-024)
- Scale: 12px caption, 14px body, 16px subtitle, 20px title, 24px heading, 32px display

**Iconography**: Material Design Icons (outlined variant) for consistency with
Angular Material. Custom medical icons for clinical-specific indicators (conflict,
coding, extraction).

**Imagery**: Minimal illustration style for empty states and onboarding. No
stock photography. SVG-based illustrations with brand color palette.

## Component Specifications

### Actions

| Component | Variants | Used In | UXR Reference |
|-----------|----------|---------|---------------|
| Primary Button | Default, Loading, Disabled | All screens | UXR-501 |
| Secondary Button | Default, Hover, Disabled | SCR-006, SCR-007, SCR-014 | UXR-501 |
| Icon Button | Default, Hover, Active | SCR-013 (zoom, rotate), SCR-026 (print) | UXR-109 |
| Destructive Button | Default, Confirm Dialog | SCR-007 (cancel), SCR-012 (delete), SCR-017 (reject) | UXR-111 |
| FAB (Floating Action) | Default | SCR-011 (upload), SCR-029 (walk-in) | UXR-304 |

### Inputs

| Component | Variants | Used In | UXR Reference |
|-----------|----------|---------|---------------|
| Text Input | Default, Focus, Error, Disabled, Read-only | All forms | UXR-205 |
| Password Input | Default, Show/Hide toggle | SCR-002, SCR-003 | UXR-202 |
| Date Picker | Single date, Date range | SCR-004, SCR-007, SCR-021, SCR-023 | UXR-103 |
| Dropdown Select | Single, Multi, Searchable | SCR-004, SCR-019, SCR-020 | UXR-202 |
| Toggle Switch | On/Off | SCR-005 (AI assist), SCR-009 (channels) | UXR-104 |
| Checkbox | Single, Group | SCR-009 (timings), SCR-020 (bulk select) | UXR-202 |
| File Upload Zone | Drag-drop, Browse button | SCR-011, SCR-028 | UXR-505 |
| Search Input | With autocomplete dropdown | SCR-018, SCR-020 | UXR-506 |
| Code Editor | HTML syntax highlighting | SCR-024 | UXR-202 |

### Navigation

| Component | Variants | Used In | UXR Reference |
|-----------|----------|---------|---------------|
| Sidebar Nav | Expanded (240px), Collapsed (64px) | SCR-030 | UXR-302 |
| Hamburger Menu | Mobile drawer | SCR-030 | UXR-302 |
| Tab Bar | Horizontal, Scrollable (mobile) | SCR-014, SCR-019, SCR-024 | UXR-202 |
| Breadcrumbs | Standard | SCR-030 | UXR-202 |
| Pagination | Numbered, Previous/Next | SCR-007, SCR-012, SCR-020, SCR-021 | UXR-303 |
| Step Indicator | Horizontal, Numbered | SCR-001, SCR-003, SCR-027 | UXR-101 |

### Content

| Component | Variants | Used In | UXR Reference |
|-----------|----------|---------|---------------|
| Data Table | Standard, Expandable row, Card-on-mobile | SCR-007, SCR-012, SCR-020, SCR-021, SCR-025 | UXR-303 |
| Card | Standard, Suggestion, Alert, KPI metric | SCR-006, SCR-008, SCR-016, SCR-017, SCR-023 | UXR-403 |
| Timeline | Vertical, Filtered | SCR-015 | UXR-107 |
| Badge | Status, Role, Confidence, AI marker | SCR-014, SCR-017, SCR-025 | UXR-404, UXR-405 |
| Progress Bar | Linear (upload), Circular (processing) | SCR-011, SCR-017, SCR-022 | UXR-505 |
| Skeleton Loader | Row, Card, Chart, Document | All screens (loading states) | UXR-501 |
| Empty State | Illustration + message + CTA | All screens (empty states) | UXR-603 |
| Chart | Line, Bar, Donut | SCR-023 | UXR-201 |

### Feedback

| Component | Variants | Used In | UXR Reference |
|-----------|----------|---------|---------------|
| Toast | Success (auto-dismiss 5s), Error (persistent), Info | All screens | UXR-502 |
| Confirmation Dialog | Standard, Destructive action, Typed confirmation | SCR-007, SCR-012, SCR-016, SCR-017, SCR-020 | UXR-111 |
| Banner | Error retry, Connection lost, Warning | SCR-004, SCR-025 | UXR-602 |
| Alert Card | Severity-coded (critical, high, moderate, low) | SCR-016 | UXR-504 |
| Session Timeout Modal | Warning with extend/logout | SCR-030 | UXR-102 |
| Tooltip | Standard, Rich (with code descriptions) | SCR-017, SCR-018 | UXR-204 |
| Loading Spinner | Button inline, Full-page overlay | All screens | UXR-501 |

## Prototype Flows

### FL-001: Patient Registration and Login

**Personas**: Patient
**Epic**: EP-001
**Use Case**: UC-001

```
SCR-002 (Login)
  ├── [New User] → SCR-001 (Registration) → Step 1 → Step 2 → Step 3 → SCR-002 (Login)
  ├── [Forgot Password] → SCR-003 (Password Reset) → SCR-002 (Login)
  └── [Valid Credentials] → SCR-030 (App Shell → Patient Dashboard)
```

**Key Interactions**: Login form submission, verification code entry,
session creation, role-based redirect.

---

### FL-002: Appointment Booking (Patient)

**Personas**: Patient
**Epic**: EP-002
**Use Case**: UC-002

```
SCR-030 (Dashboard) → SCR-004 (Slot Search)
  ├── [Slot Selected] → SCR-005 (Intake Form) → SCR-006 (Booking Confirmation)
  │   └── [Download PDF / ICS / QR]
  ├── [No Slots] → SCR-008 (Waitlist Join)
  └── [Staff Override Path] → SCR-027 (Staff Booking)
```

**Key Interactions**: Date/duration filter, slot selection, intake autosave,
booking confirmation, artifact download.

---

### FL-003: Staff Queue Management

**Personas**: Staff
**Epic**: EP-004
**Use Case**: UC-002, UC-003

```
SCR-030 (Staff Dashboard) → SCR-025 (Queue Dashboard)
  ├── [Check-in Patient] → Status transition → Queue reorder
  ├── [Walk-in] → SCR-029 (Walk-in Registration) → Queue insertion
  ├── [View Schedule] → SCR-026 (Daily Schedule) → Drag-and-drop reorder
  └── [Book for Patient] → SCR-027 (Staff Booking) → SCR-006 (Confirmation)
```

**Key Interactions**: One-click check-in, walk-in quick entry, drag-and-drop
schedule, override with reason.

---

### FL-004: Document Upload and Processing

**Personas**: Patient, Staff, Clinician
**Epic**: EP-006
**Use Case**: UC-004

```
SCR-030 (Dashboard) → SCR-011 (Document Upload)
  └── [Upload Complete] → SCR-012 (Document Library)
      └── [View Document] → SCR-013 (Document Viewer)
          └── [Extracted Entities] → SCR-014 (Patient Profile)
```

**Key Interactions**: Drag-and-drop upload, progress tracking, OCR status
monitoring, document viewing with extracted entities sidebar.

---

### FL-005: Clinical Review and Coding

**Personas**: Clinician
**Epic**: EP-007, EP-008
**Use Case**: UC-005

```
SCR-030 (Clinician Dashboard) → SCR-014 (360° Patient Profile)
  ├── [Timeline Tab] → SCR-015 (Clinical Timeline)
  ├── [Conflicts Detected] → SCR-016 (Conflict Alerts) → Acknowledge
  ├── [Coding Tab] → SCR-017 (Coding Suggestion Review)
  │   ├── [Accept/Modify/Reject] → Finalize
  │   └── [Manual Code] → SCR-018 (Code Search)
  └── [Documents Tab] → SCR-013 (Document Viewer)
```

**Key Interactions**: Tab navigation, conflict acknowledgment, coding
decision workflow, code search with autocomplete.

---

### FL-006: Admin Platform Configuration

**Personas**: Admin
**Epic**: EP-010, EP-011
**Use Case**: UC-006

```
SCR-030 (Admin Dashboard) → SCR-023 (KPI Dashboard)
  ├── [Users] → SCR-020 (User Management) → Invite / Deactivate / Bulk action
  ├── [Config] → SCR-019 (System Configuration) → Edit / Save
  ├── [Templates] → SCR-024 (Template Editor) → Edit / Preview / Version
  ├── [Audit] → SCR-021 (Audit Log Viewer) → Filter / Export
  └── [Reports] → SCR-022 (Compliance Reports) → Generate / Download
```

**Key Interactions**: KPI chart interaction, user bulk actions, config
validation, template live preview, audit log filtering.

---

### FL-007: Error Recovery

**Personas**: All
**Epic**: Cross-cutting

```
[Any Screen] → Network Error
  └── Retry Banner → [Retry Action] → [Success] → Resume flow
                                     → [Failure] → Persistent error with support link

[Any Screen] → Session Timeout Warning (SCR-030 modal)
  ├── [Extend] → Session refreshed → Continue
  └── [Logout / Timeout] → SCR-002 (Login)

[SCR-017] → AI Service Failure
  └── Fallback Banner → SCR-018 (Manual Code Search)
```

**Key Interactions**: Retry mechanism, session extension, graceful degradation
from AI to manual workflows.

## Export Requirements

### Breakpoints

| Breakpoint | Width | Target |
|------------|-------|--------|
| Mobile | 375px | Phone (portrait) |
| Tablet | 768px | Tablet (portrait) |
| Desktop | 1440px | Standard desktop |

### Asset Export Specifications

| Asset Type | Format | Scale | Naming Convention |
|------------|--------|-------|-------------------|
| Icons | SVG | 1x | `icon-{name}.svg` |
| Illustrations | SVG | 1x | `illus-{context}.svg` |
| Logo | SVG + PNG | 1x, 2x | `logo-{variant}.{ext}` |
| Favicons | PNG, ICO | 16, 32, 180 | `favicon-{size}.png` |

### Spacing System

| Token | Value | Usage |
|-------|-------|-------|
| space-xs | 4px | Inline icon spacing |
| space-sm | 8px | Compact component padding |
| space-md | 16px | Standard component padding, form field gap |
| space-lg | 24px | Section spacing, card padding |
| space-xl | 32px | Page section dividers |
| space-2xl | 48px | Major section separation |

## Figma File Structure

```
Unified Patient Access Platform
├── 📄 Cover Page
├── 📁 Design System
│   ├── Colors (primary, secondary, neutral, semantic, AI accent)
│   ├── Typography (Inter scale, JetBrains Mono)
│   ├── Spacing (4px base grid)
│   ├── Elevation (3-level shadow system)
│   ├── Icons (Material Design outlined + custom medical)
│   └── Component Library
│       ├── Actions (buttons, FAB)
│       ├── Inputs (text, password, date, select, toggle, upload, search, code editor)
│       ├── Navigation (sidebar, tabs, breadcrumbs, pagination, steps)
│       ├── Content (table, card, timeline, badge, progress, skeleton, empty state, chart)
│       └── Feedback (toast, dialog, banner, alert card, timeout modal, tooltip, spinner)
├── 📁 Auth Screens
│   ├── SCR-001 Registration (5 states × 3 breakpoints)
│   ├── SCR-002 Login (5 states × 3 breakpoints)
│   └── SCR-003 Password Reset (5 states × 3 breakpoints)
├── 📁 Scheduling Screens
│   ├── SCR-004 Slot Search (5 states × 3 breakpoints)
│   ├── SCR-005 Intake Form (5 states × 3 breakpoints)
│   ├── SCR-006 Booking Confirmation (5 states × 3 breakpoints)
│   ├── SCR-007 Appointment History (5 states × 3 breakpoints)
│   └── SCR-008 Waitlist (5 states × 3 breakpoints)
├── 📁 Notification Screens
│   └── SCR-009 Notification Preferences (5 states × 3 breakpoints)
├── 📁 Document Screens
│   ├── SCR-011 Document Upload (5 states × 3 breakpoints)
│   ├── SCR-012 Document Library (5 states × 3 breakpoints)
│   └── SCR-013 Document Viewer (5 states × 3 breakpoints)
├── 📁 Clinical Screens
│   ├── SCR-014 360° Patient Profile (5 states × 3 breakpoints)
│   ├── SCR-015 Clinical Timeline (5 states × 3 breakpoints)
│   ├── SCR-016 Conflict Alerts (5 states × 3 breakpoints)
│   ├── SCR-017 Coding Suggestion Review (5 states × 3 breakpoints)
│   └── SCR-018 Code Search (5 states × 3 breakpoints)
├── 📁 Staff Screens
│   ├── SCR-025 Queue Dashboard (5 states × 3 breakpoints)
│   ├── SCR-026 Daily Schedule (5 states × 3 breakpoints)
│   ├── SCR-027 Staff Booking (5 states × 3 breakpoints)
│   ├── SCR-028 Insurance Verification (5 states × 3 breakpoints)
│   └── SCR-029 Walk-in Registration (5 states × 3 breakpoints)
├── 📁 Admin Screens
│   ├── SCR-019 System Configuration (5 states × 3 breakpoints)
│   ├── SCR-020 User Management (5 states × 3 breakpoints)
│   ├── SCR-021 Audit Log Viewer (5 states × 3 breakpoints)
│   ├── SCR-022 Compliance Reports (5 states × 3 breakpoints)
│   ├── SCR-023 KPI Dashboard (5 states × 3 breakpoints)
│   └── SCR-024 Template Editor (5 states × 3 breakpoints)
├── 📁 Shell
│   └── SCR-030 App Shell (5 states × 3 breakpoints)
└── 📁 Prototype Flows
    ├── FL-001 Registration and Login
    ├── FL-002 Appointment Booking
    ├── FL-003 Staff Queue Management
    ├── FL-004 Document Upload and Processing
    ├── FL-005 Clinical Review and Coding
    ├── FL-006 Admin Configuration
    └── FL-007 Error Recovery
```
