# Component Inventory - Unified Patient Access and Clinical Intelligence Platform

## Component Specification

**Fidelity Level**: High
**Screen Type**: Responsive Web
**Viewport**: 375px / 768px / 1440px

## Component Summary

| Component Name | Type | Screens Used | Priority | Implementation Status |
|---------------|------|-------------|----------|---------------------|
| Application Shell | Layout | SCR-030 and all authenticated screens | High | Pending |
| Auth Stack | Layout | SCR-001, SCR-002, SCR-003 | High | Pending |
| Workflow Footer | Layout | SCR-004, SCR-005, SCR-027 | High | Pending |
| Sidebar Navigation | Navigation | SCR-019 to SCR-030 | High | Pending |
| Breadcrumb Header | Navigation | Authenticated screens | High | Pending |
| Step Indicator | Navigation | SCR-001, SCR-003, SCR-027 | High | Pending |
| Tab Strip | Navigation | SCR-014, SCR-019, SCR-024 | Medium | Pending |
| Filter Toolbar | Content | SCR-004, SCR-007, SCR-012, SCR-020, SCR-021, SCR-023 | High | Pending |
| Data Grid | Content | SCR-007, SCR-012, SCR-020, SCR-021, SCR-025 | High | Pending |
| Timeline Rail | Content | SCR-015 | Medium | Pending |
| Suggestion Card | Content | SCR-017 | High | Pending |
| KPI Card | Content | SCR-023 | Medium | Pending |
| Button Family | Interactive | All screens | High | Pending |
| Form Input Family | Interactive | All forms | High | Pending |
| File Upload Zone | Interactive | SCR-011, SCR-028 | High | Pending |
| Search Autocomplete | Interactive | SCR-018 | Medium | Pending |
| Confirmation Dialog | Feedback | SCR-007, SCR-012, SCR-016, SCR-017, SCR-020 | High | Pending |
| Status Banner | Feedback | All screens | High | Pending |
| Toast Stack | Feedback | All screens | Medium | Pending |
| Empty State Panel | Feedback | Table and search surfaces | Medium | Pending |

## Detailed Component Specifications

### Layout Components

#### Application Shell
- **Type**: Layout
- **Used In Screens**: SCR-014 to SCR-030
- **Wireframe References**:
  - [wireframe-SCR-014-patient-profile-360.html](./Hi-Fi/wireframe-SCR-014-patient-profile-360.html)
  - [wireframe-SCR-023-kpi-dashboard.html](./Hi-Fi/wireframe-SCR-023-kpi-dashboard.html)
  - [wireframe-SCR-030-application-shell.html](./Hi-Fi/wireframe-SCR-030-application-shell.html)
- **Description**: Persistent sidebar, top bar, breadcrumbs, content canvas,
  and auxiliary utility rail for alerts and profile actions.
- **Variants**: Desktop expanded, desktop collapsed, mobile drawer.
- **Interactive States**: Default, hover, active, focus, loading.
- **Responsive Behavior**:
  - Desktop (1440px): Fixed sidebar with split content regions.
  - Tablet (768px): Sidebar collapses to icon rail.
  - Mobile (375px): Full drawer navigation with sticky header.
- **Implementation Notes**: Align with role-based routing and session timeout
  warnings.

#### Auth Stack
- **Type**: Layout
- **Used In Screens**: SCR-001, SCR-002, SCR-003
- **Wireframe References**:
  - [wireframe-SCR-001-patient-registration.html](./Hi-Fi/wireframe-SCR-001-patient-registration.html)
  - [wireframe-SCR-002-login.html](./Hi-Fi/wireframe-SCR-002-login.html)
  - [wireframe-SCR-003-password-reset.html](./Hi-Fi/wireframe-SCR-003-password-reset.html)
- **Description**: Centered authentication frame with branded masthead and
  progress or recovery helper copy.
- **Variants**: Step-based, single-step, confirmation state.
- **Interactive States**: Default, loading, validation, error.
- **Responsive Behavior**:
  - Desktop (1440px): Centered card with ambient hero panel.
  - Tablet (768px): Simplified card spacing.
  - Mobile (375px): Full-width card, compact header.
- **Implementation Notes**: Preserve the 30-second completion goal for
  registration.

### Navigation Components

#### Sidebar Navigation
- **Type**: Navigation
- **Used In Screens**: SCR-019 to SCR-030
- **Wireframe References**:
  - [wireframe-SCR-019-system-configuration.html](./Hi-Fi/wireframe-SCR-019-system-configuration.html)
  - [wireframe-SCR-025-queue-dashboard.html](./Hi-Fi/wireframe-SCR-025-queue-dashboard.html)
  - [wireframe-SCR-030-application-shell.html](./Hi-Fi/wireframe-SCR-030-application-shell.html)
- **Description**: Role-aware section navigation with icon plus label pairs.
- **Variants**: Expanded, collapsed, drawer.
- **Interactive States**: Default, hover, active, focus.
- **Responsive Behavior**:
  - Desktop (1440px): 240px full-width sidebar.
  - Tablet (768px): 72px icon rail.
  - Mobile (375px): Slide-out drawer.
- **Implementation Notes**: Keep active route contrast above AA.

#### Step Indicator
- **Type**: Navigation
- **Used In Screens**: SCR-001, SCR-003, SCR-027
- **Wireframe References**:
  - [wireframe-SCR-001-patient-registration.html](./Hi-Fi/wireframe-SCR-001-patient-registration.html)
  - [wireframe-SCR-027-staff-assisted-booking.html](./Hi-Fi/wireframe-SCR-027-staff-assisted-booking.html)
- **Description**: Numbered progress tracker for wizard flows.
- **Variants**: Three-step patient flow, four-step staff flow.
- **Interactive States**: Complete, current, pending.
- **Responsive Behavior**:
  - Desktop (1440px): Horizontal labels and status text.
  - Tablet (768px): Compact horizontal chips.
  - Mobile (375px): Compressed numbered rail.
- **Implementation Notes**: Communicate progress textually, not only by color.

### Content Components

#### Data Grid
- **Type**: Content
- **Used In Screens**: SCR-007, SCR-012, SCR-020, SCR-021, SCR-025
- **Wireframe References**:
  - [wireframe-SCR-007-appointment-history.html](./Hi-Fi/wireframe-SCR-007-appointment-history.html)
  - [wireframe-SCR-020-user-management.html](./Hi-Fi/wireframe-SCR-020-user-management.html)
  - [wireframe-SCR-025-queue-dashboard.html](./Hi-Fi/wireframe-SCR-025-queue-dashboard.html)
- **Description**: Dense table or list surface with filters, actions, and row
  detail expansion.
- **Variants**: Standard table, operational queue, read-only audit list.
- **Interactive States**: Default, hover, selected, loading, empty.
- **Responsive Behavior**:
  - Desktop (1440px): Full table with sticky headers.
  - Tablet (768px): Reduced columns.
  - Mobile (375px): Card stack replacing columns.
- **Implementation Notes**: Support keyboard row traversal and bulk select.

#### Suggestion Card
- **Type**: Content
- **Used In Screens**: SCR-017
- **Wireframe References**:
  - [wireframe-SCR-017-coding-suggestion-review.html](./Hi-Fi/wireframe-SCR-017-coding-suggestion-review.html)
- **Description**: AI code recommendation surface with confidence bar,
  rationale, and accept/modify/reject actions.
- **Variants**: ICD-10, CPT, accepted, rejected, editable.
- **Interactive States**: Default, focus, selected, disabled.
- **Responsive Behavior**:
  - Desktop (1440px): Dual-column code sections.
  - Tablet (768px): Single-column groups.
  - Mobile (375px): Full-width stacked cards.
- **Implementation Notes**: Retain source traceability and manual fallback.

### Interactive Components

#### Button Family
- **Type**: Interactive
- **Used In Screens**: All screens
- **Wireframe References**:
  - [wireframe-SCR-004-slot-search-discovery.html](./Hi-Fi/wireframe-SCR-004-slot-search-discovery.html)
  - [wireframe-SCR-011-document-upload.html](./Hi-Fi/wireframe-SCR-011-document-upload.html)
  - [wireframe-SCR-024-template-editor.html](./Hi-Fi/wireframe-SCR-024-template-editor.html)
- **Description**: Primary, secondary, destructive, icon, and FAB treatments.
- **Variants**: Default, loading, disabled, icon-only.
- **Interactive States**: Default, hover, active, focus, disabled, loading.
- **Responsive Behavior**:
  - Desktop (1440px): Inline action groups.
  - Tablet (768px): Wrapping actions.
  - Mobile (375px): Full-width primary action when critical.
- **Implementation Notes**: Use the design token radius and transition set.

#### File Upload Zone
- **Type**: Interactive
- **Used In Screens**: SCR-011, SCR-028
- **Wireframe References**:
  - [wireframe-SCR-011-document-upload.html](./Hi-Fi/wireframe-SCR-011-document-upload.html)
  - [wireframe-SCR-028-insurance-verification.html](./Hi-Fi/wireframe-SCR-028-insurance-verification.html)
- **Description**: Drag-and-drop input with progress feedback and retry affordance.
- **Variants**: Empty, uploading, scanned, failed.
- **Interactive States**: Default, drag-over, loading, error, complete.
- **Responsive Behavior**:
  - Desktop (1440px): Large centered drop surface.
  - Tablet (768px): Reduced padding.
  - Mobile (375px): Tap-first upload card.
- **Implementation Notes**: Reserve space for OCR and malware status badges.

### Feedback Components

#### Status Banner
- **Type**: Feedback
- **Used In Screens**: All screens with network, validation, or service risk
- **Wireframe References**:
  - [wireframe-SCR-002-login.html](./Hi-Fi/wireframe-SCR-002-login.html)
  - [wireframe-SCR-016-conflict-alerts.html](./Hi-Fi/wireframe-SCR-016-conflict-alerts.html)
  - [wireframe-SCR-025-queue-dashboard.html](./Hi-Fi/wireframe-SCR-025-queue-dashboard.html)
- **Description**: Top-level banner with semantic color treatment and retry or
  acknowledgment actions.
- **Variants**: Info, success, warning, error, critical.
- **Interactive States**: Default, dismissible, persistent.
- **Responsive Behavior**:
  - Desktop (1440px): Inline action on right edge.
  - Tablet (768px): Wrapped action row.
  - Mobile (375px): Full-width stacked layout.
- **Implementation Notes**: Critical alerts remain persistent until actioned.

## Component Relationships

```text
Application Shell
+-- Header
|   +-- Breadcrumbs
|   +-- Context Actions
|   +-- User Menu
+-- Sidebar Navigation
+-- Status Banner
+-- Page Section
    +-- Toolbar
    +-- Data Region
    |   +-- Cards / Table / Timeline / Editor / Viewer
    +-- Feedback Region
        +-- Toasts / Inline Errors / Empty State
```

## Component States Matrix

| Component | Default | Hover | Active | Focus | Disabled | Error | Loading | Empty |
|-----------|---------|-------|--------|-------|----------|-------|---------|-------|
| Button | x | x | x | x | x | - | x | - |
| Input Field | x | x | x | x | x | x | - | x |
| Step Indicator | x | - | x | x | - | - | - | - |
| Data Grid | x | x | x | x | - | x | x | x |
| Upload Zone | x | x | x | x | x | x | x | x |
| Suggestion Card | x | x | x | x | - | x | x | x |
| Banner | x | - | - | x | - | x | - | - |

## Reusability Analysis

| Component | Reuse Count | Screens | Recommendation |
|-----------|-------------|---------|----------------|
| Application Shell | 12+ | Operational and governance screens | Create shared shell component |
| Data Grid | 5 | Scheduling, documents, users, audits, queue | Create shared data-grid with mobile card variant |
| Status Banner | 20+ | Cross-cutting | Create shared feedback primitive |
| Form Input Family | 15+ | Registration, booking, settings, admin forms | Use shared form field library |
| Suggestion Card | 1 | Coding review | Keep feature-specific, but derive from shared card |

## Responsive Breakpoints Summary

| Breakpoint | Width | Components Affected | Key Adaptations |
|-----------|-------|-------------------|-----------------|
| Mobile | 375px | Sidebar, tables, split views, toolbars | Drawer nav, card stacks, bottom sheets, full-width actions |
| Tablet | 768px | Dashboards, filters, split editors | Compressed sidebar, two-column layouts, wrapped toolbars |
| Desktop | 1440px | Shell, tables, viewer/editor | Persistent nav, dense layouts, side panels |

## Implementation Priority Matrix

### High Priority (Core Components)
- [ ] Application Shell - shared navigation and layout foundation.
- [ ] Form Input Family - required by registration, intake, and admin settings.
- [ ] Button Family - core action affordance across all workflows.
- [ ] Data Grid - supports the operational views with highest density.

### Medium Priority (Feature Components)
- [ ] Suggestion Card - required for clinician coding review.
- [ ] Timeline Rail - required for clinical chronology views.
- [ ] KPI Card - required for admin analytics.

### Low Priority (Enhancement Components)
- [ ] Decorative empty-state illustrations - useful but not structurally blocking.

## Framework-Specific Notes

**Detected Framework**: Angular
**Component Library**: Custom app components with Angular Material-style interaction patterns

### Framework Patterns Applied
- Signal-friendly data regions with responsive layout shells.
- Accessible table and grid navigation for dense operational views.

### Component Library Mappings

| Wireframe Component | Framework Component | Customization Required |
|-------------------|-------------------|----------------------|
| Button Family | `app-button`, `app-icon-button`, `app-fab` | Variants, loading, destructive emphasis |
| Form Input Family | `app-input`, `app-select`, `app-date-picker` | Validation, helper text, keyboard focus treatment |
| Data Grid | custom table plus Angular ARIA grid pattern | Row expansion, selection, responsive card fallback |
| Tab Strip | `app-tabs` | Scrollable mobile behavior |
| Upload Zone | custom upload component | OCR and scan status, progress, retry |

## Accessibility Considerations

| Component | ARIA Attributes | Keyboard Navigation | Screen Reader Notes |
|-----------|----------------|-------------------|-------------------|
| Sidebar Navigation | `nav`, `aria-current` | Tab and arrow navigation | Active destination announced clearly |
| Step Indicator | `aria-label`, `aria-current="step"` | Sequential tab order | Current step and completion state announced |
| Data Grid | `role="grid"`, `aria-rowcount` | Arrow-key row and cell traversal | Sort and expansion state announced |
| Upload Zone | `aria-describedby`, `role="button"` | Enter and Space for browse | Progress and failure states announced live |
| Dialog | `role="dialog"`, `aria-modal="true"` | Focus trap, Escape to close | Title and consequence copy read first |

## Design System Integration

**Design System Reference**: `.propel/context/docs/designsystem.md`

### Components Matching Design System
- [x] Button Family - uses primary, secondary, and destructive token mappings.
- [x] Form Input Family - uses radius, spacing, and focus ring tokens.
- [x] Status Banner - uses semantic color tokens and consistent elevation.
- [x] KPI Card - uses neutral surface, shadow, and subtitle scale.

### New Components to Add to Design System
- [ ] Queue row urgency indicator - operational micro-pattern worth standardizing.
- [ ] AI rationale block - feature-specific pattern that may expand beyond coding review.