# Wireframe Reference - Unified Patient Access and Clinical Intelligence Platform

## Wireframe Specification

**Fidelity Level**: High
**Platform**: Responsive Web Application
**Primary Viewports**: 375px mobile, 768px tablet, 1440px desktop
**Source Inputs**:
- `.propel/context/docs/figma_spec.md`
- `.propel/context/docs/designsystem.md`
- `.propel/context/docs/models.md`

## System Overview

This wireframe set covers the role-based web experience for patients, staff,
clinicians, and administrators. The generated screens follow a shared
application shell with responsive adaptations and use the healthcare visual
system defined in the project design system. High-risk flows emphasize clear
confirmation patterns, persistent status messaging, and traceability for
AI-assisted decisions.

## Generated Wireframes

| Screen ID | Screen Name | Primary Persona | Output |
|-----------|-------------|-----------------|--------|
| SCR-001 | Patient Registration | Patient | `Hi-Fi/wireframe-SCR-001-patient-registration.html` |
| SCR-002 | Login | All | `Hi-Fi/wireframe-SCR-002-login.html` |
| SCR-003 | Password Reset | All | `Hi-Fi/wireframe-SCR-003-password-reset.html` |
| SCR-004 | Slot Search and Discovery | Patient, Staff | `Hi-Fi/wireframe-SCR-004-slot-search-discovery.html` |
| SCR-005 | Intake Form | Patient, Staff | `Hi-Fi/wireframe-SCR-005-intake-form.html` |
| SCR-006 | Booking Confirmation | Patient, Staff | `Hi-Fi/wireframe-SCR-006-booking-confirmation.html` |
| SCR-007 | Appointment History | Patient, Staff | `Hi-Fi/wireframe-SCR-007-appointment-history.html` |
| SCR-008 | Waitlist View | Patient, Staff | `Hi-Fi/wireframe-SCR-008-waitlist-view.html` |
| SCR-009 | Notification Preferences | Patient | `Hi-Fi/wireframe-SCR-009-notification-preferences.html` |
| SCR-011 | Document Upload | Patient, Staff | `Hi-Fi/wireframe-SCR-011-document-upload.html` |
| SCR-012 | Document Library | Patient, Staff, Clinician | `Hi-Fi/wireframe-SCR-012-document-library.html` |
| SCR-013 | Document Viewer | Patient, Staff, Clinician | `Hi-Fi/wireframe-SCR-013-document-viewer.html` |
| SCR-014 | 360-Degree Patient Profile | Staff, Clinician | `Hi-Fi/wireframe-SCR-014-patient-profile-360.html` |
| SCR-015 | Clinical Timeline | Staff, Clinician | `Hi-Fi/wireframe-SCR-015-clinical-timeline.html` |
| SCR-016 | Conflict Alerts | Staff, Clinician | `Hi-Fi/wireframe-SCR-016-conflict-alerts.html` |
| SCR-017 | Coding Suggestion Review | Clinician | `Hi-Fi/wireframe-SCR-017-coding-suggestion-review.html` |
| SCR-018 | Code Search | Clinician | `Hi-Fi/wireframe-SCR-018-code-search.html` |
| SCR-019 | System Configuration | Admin | `Hi-Fi/wireframe-SCR-019-system-configuration.html` |
| SCR-020 | User Management | Admin | `Hi-Fi/wireframe-SCR-020-user-management.html` |
| SCR-021 | Audit Log Viewer | Admin | `Hi-Fi/wireframe-SCR-021-audit-log-viewer.html` |
| SCR-022 | Compliance Reports | Admin | `Hi-Fi/wireframe-SCR-022-compliance-reports.html` |
| SCR-023 | KPI Dashboard | Admin | `Hi-Fi/wireframe-SCR-023-kpi-dashboard.html` |
| SCR-024 | Template Editor | Admin | `Hi-Fi/wireframe-SCR-024-template-editor.html` |
| SCR-025 | Queue Dashboard | Staff | `Hi-Fi/wireframe-SCR-025-queue-dashboard.html` |
| SCR-026 | Daily Schedule View | Staff | `Hi-Fi/wireframe-SCR-026-daily-schedule-view.html` |
| SCR-027 | Staff-Assisted Booking | Staff | `Hi-Fi/wireframe-SCR-027-staff-assisted-booking.html` |
| SCR-028 | Insurance Verification | Patient, Staff | `Hi-Fi/wireframe-SCR-028-insurance-verification.html` |
| SCR-029 | Walk-in Registration | Staff | `Hi-Fi/wireframe-SCR-029-walk-in-registration.html` |
| SCR-030 | Application Shell | All | `Hi-Fi/wireframe-SCR-030-application-shell.html` |

## Personas and Core Flows

| Persona | Primary Flow | Screens |
|---------|--------------|---------|
| Patient | Register, authenticate, find a slot, complete intake, confirm visit | SCR-001, SCR-002, SCR-004, SCR-005, SCR-006 |
| Patient | Review appointments, manage waitlist, set reminder preferences | SCR-007, SCR-008, SCR-009 |
| Patient | Upload and review documents, verify insurance | SCR-011, SCR-012, SCR-013, SCR-028 |
| Staff | Manage queue, daily operations, assisted booking, walk-in intake | SCR-025, SCR-026, SCR-027, SCR-029 |
| Staff | Access patient lookup, documents, and insurance verification | SCR-012, SCR-013, SCR-014, SCR-015, SCR-028 |
| Clinician | Review clinical context, conflicts, and coding recommendations | SCR-014, SCR-015, SCR-016, SCR-017, SCR-018 |
| Admin | Configure system behavior, govern users, and monitor operations | SCR-019, SCR-020, SCR-021, SCR-022, SCR-023, SCR-024 |

## Screen Hierarchy

```text
SCR-030 Application Shell
+-- Patient Portal
|   +-- SCR-004 Slot Search and Discovery
|   +-- SCR-005 Intake Form
|   +-- SCR-006 Booking Confirmation
|   +-- SCR-007 Appointment History
|   +-- SCR-008 Waitlist View
|   +-- SCR-009 Notification Preferences
|   +-- SCR-011 Document Upload
|   +-- SCR-012 Document Library
|   +-- SCR-013 Document Viewer
|   +-- SCR-028 Insurance Verification
+-- Staff Portal
|   +-- SCR-025 Queue Dashboard
|   +-- SCR-026 Daily Schedule View
|   +-- SCR-027 Staff-Assisted Booking
|   +-- SCR-029 Walk-in Registration
|   +-- SCR-014 360-Degree Patient Profile
|   +-- SCR-015 Clinical Timeline
+-- Clinician Portal
|   +-- SCR-014 360-Degree Patient Profile
|   +-- SCR-015 Clinical Timeline
|   +-- SCR-016 Conflict Alerts
|   +-- SCR-017 Coding Suggestion Review
|   +-- SCR-018 Code Search
+-- Admin Portal
    +-- SCR-023 KPI Dashboard
    +-- SCR-020 User Management
    +-- SCR-019 System Configuration
    +-- SCR-024 Template Editor
    +-- SCR-021 Audit Log Viewer
    +-- SCR-022 Compliance Reports
```

## Modal and Overlay Inventory

| Pattern | Used In | Purpose |
|---------|---------|---------|
| Session timeout modal | SCR-030 | Warn before forced logout and offer extend session action |
| Confirmation dialog | SCR-007, SCR-012, SCR-016, SCR-017, SCR-020 | Guard destructive or high-impact actions |
| Print preview modal | SCR-015, SCR-026 | Preview print-optimized layouts before printing |
| Inline side panel | SCR-013, SCR-014, SCR-020 | Show supporting detail without full page navigation |
| Autocomplete overlay | SCR-018 | Provide fast code lookup with keyboard support |
| Drawer navigation | SCR-030 | Mobile navigation pattern below 768px |

## Navigation Architecture

The shell uses a persistent left sidebar on desktop with a compact header,
breadcrumbs, and a content canvas. Patient screens prefer focused layouts and
shorter breadcrumb trails. Staff and admin screens emphasize dense tables,
toolbars, and side panels. Clinician views give extra space to source-backed
content and decision support panels.

## Interaction Patterns

| Pattern | Application |
|---------|-------------|
| Sticky action footer | SCR-004, SCR-005, SCR-027 for multi-step flows |
| Inline validation | All forms with `aria-describedby` error messaging |
| Toast feedback | Success messages auto-dismiss, error messages persist |
| Row expansion | SCR-020, SCR-021, SCR-025 for detail-on-demand workflows |
| State badges | Queue, risk, OCR, verification, AI, and severity indicators |
| Split-view editing | SCR-013 and SCR-024 keep source and action surface visible together |

## Error Handling Strategy

All error-prone screens reserve a top-of-view banner zone for network and
service failures. Local validation errors stay inline next to their triggering
field. AI-dependent screens provide a clear fallback path to manual review or
manual entry. Empty states combine an explanatory sentence with a primary next
action to prevent dead ends.

## Responsive Strategy

| Breakpoint | Layout Behavior |
|-----------|-----------------|
| 375px | Sidebar collapses to drawer, tables become stacked cards, secondary panels become sheets or accordions |
| 768px | Two-column detail views remain possible, filters compress, action rails wrap to multiple lines |
| 1440px | Full shell with persistent sidebar, dense tables, split-view workspaces, and dashboard grids |

## Accessibility Strategy

- All interactive controls expose visible focus rings and clear text labels.
- Modal flows assume focus trap and return-to-trigger behavior.
- Dynamic updates such as queue movement, upload status, and toast messages are
  announced via live regions.
- Tables with high interaction density are designed to map cleanly to keyboard
  and screen-reader grid navigation patterns.

## Content Strategy

The content model favors concise, reassuring language for patient flows and a
more operational tone for staff, clinician, and admin views. AI-generated
outputs are explicitly labeled and paired with rationale or source references.
Critical alerts use direct language and severity coding without introducing
alarmist copy.