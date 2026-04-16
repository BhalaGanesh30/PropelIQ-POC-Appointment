## Design Reference

## UI Impact Assessment

**Has UI Changes**: [x] Yes [ ] No

## User Story Design Context

**Story ID**: Platform-wide
**Story Title**: Unified Patient Access and Clinical Intelligence Platform
**UI Impact Type**: New UI

### Design Source References

- **Design System**: This document (`designsystem.md`)
- **Figma Specification**: `.propel/context/docs/figma_spec.md`
- **Brand Guidelines**: See Branding section below

### Design Tokens

```yaml
colors:
  primary:
    50: "#E3F2FD"
    100: "#BBDEFB"
    200: "#90CAF9"
    300: "#64B5F6"
    400: "#42A5F5"
    500: "#1976D2"
    600: "#1565C0"
    700: "#0D47A1"
    usage: "Primary CTAs, active navigation, links, focus rings"
    affected_components:
      - "PrimaryButton"
      - "SidebarNav active item"
      - "TabBar active tab"
      - "Link"
      - "Focus ring"

  secondary:
    50: "#E0F2F1"
    100: "#B2DFDB"
    300: "#80CBC4"
    500: "#26A69A"
    700: "#00897B"
    usage: "Secondary CTAs, success accents, teal highlights"
    affected_components:
      - "SecondaryButton"
      - "Badge (verified)"
      - "Toggle active state"

  neutral:
    white: "#FFFFFF"
    50: "#FAFAFA"
    100: "#F5F5F5"
    200: "#EEEEEE"
    300: "#E0E0E0"
    400: "#BDBDBD"
    500: "#9E9E9E"
    600: "#757575"
    700: "#616161"
    800: "#424242"
    900: "#212121"
    usage: "Backgrounds, borders, text, disabled states"
    affected_components:
      - "Card background"
      - "Table borders"
      - "Body text"
      - "Disabled inputs"

  semantic:
    success:
      light: "#E8F5E9"
      main: "#4CAF50"
      dark: "#2E7D32"
      usage: "Success toasts, confirmed status, verified badges"
    warning:
      light: "#FFF3E0"
      main: "#FF9800"
      dark: "#E65100"
      usage: "Warning banners, medium-risk badges, expiring countdowns"
    error:
      light: "#FFEBEE"
      main: "#F44336"
      dark: "#C62828"
      usage: "Error states, destructive actions, critical alerts, validation errors"
    info:
      light: "#E3F2FD"
      main: "#2196F3"
      dark: "#1565C0"
      usage: "Info toasts, low-severity alerts, helper text icons"

  ai:
    light: "#EDE7F6"
    main: "#7E57C2"
    dark: "#4527A0"
    usage: "AI-generated content badges, AI-assisted field tints, confidence indicators"
    affected_components:
      - "AI Badge"
      - "Suggestion Card header"
      - "Confidence bar fill"

  background:
    page: "#FAFAFA"
    surface: "#FFFFFF"
    elevated: "#FFFFFF"
    overlay: "rgba(0, 0, 0, 0.5)"

typography:
  font_family:
    primary: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
    mono: "JetBrains Mono, 'Fira Code', monospace"

  display:
    family: "Inter"
    size: "32px"
    weight: "700"
    line_height: "40px"
    letter_spacing: "-0.5px"
    used_in:
      - "Login page title"
      - "Registration header"
      - "Error pages"

  heading1:
    family: "Inter"
    size: "24px"
    weight: "600"
    line_height: "32px"
    letter_spacing: "-0.25px"
    used_in:
      - "Page headers"
      - "Modal titles"
      - "Section headers"

  heading2:
    family: "Inter"
    size: "20px"
    weight: "600"
    line_height: "28px"
    letter_spacing: "0"
    used_in:
      - "Card titles"
      - "Tab labels"
      - "Sub-section headers"

  subtitle:
    family: "Inter"
    size: "16px"
    weight: "500"
    line_height: "24px"
    used_in:
      - "Table column headers"
      - "Form section labels"
      - "KPI card labels"

  body:
    family: "Inter"
    size: "14px"
    weight: "400"
    line_height: "20px"
    used_in:
      - "Body text"
      - "Table cells"
      - "Form inputs"
      - "Dropdown items"

  caption:
    family: "Inter"
    size: "12px"
    weight: "400"
    line_height: "16px"
    used_in:
      - "Helper text"
      - "Timestamps"
      - "Badge labels"
      - "Footnotes"

  code:
    family: "JetBrains Mono"
    size: "13px"
    weight: "400"
    line_height: "20px"
    used_in:
      - "ICD-10 and CPT code display"
      - "Template editor"
      - "JSON output preview"

spacing:
  base: "4px"
  scale:
    xs: "4px"
    sm: "8px"
    md: "16px"
    lg: "24px"
    xl: "32px"
    2xl: "48px"
  grid: "8px base grid"
  affected_layouts:
    - "Form field gap: 16px"
    - "Card padding: 24px"
    - "Section margin: 32px"
    - "Page padding: 24px (mobile) / 32px (desktop)"

border_radius:
  sm: "4px"
  md: "8px"
  lg: "12px"
  full: "9999px"
  usage:
    sm: "Badges, chips, small tags"
    md: "Cards, inputs, buttons, dropdowns"
    lg: "Modals, dialogs, large containers"
    full: "Avatars, circular buttons, pills"

elevation:
  level_0:
    shadow: "none"
    usage: "Flat elements, inline content"
  level_1:
    shadow: "0 1px 3px rgba(0,0,0,0.12), 0 1px 2px rgba(0,0,0,0.06)"
    usage: "Cards, dropdowns, tooltips"
  level_2:
    shadow: "0 4px 6px rgba(0,0,0,0.1), 0 2px 4px rgba(0,0,0,0.06)"
    usage: "Modals, dialogs, floating panels"
  level_3:
    shadow: "0 10px 15px rgba(0,0,0,0.1), 0 4px 6px rgba(0,0,0,0.05)"
    usage: "Sidebar nav, drawer overlay"

transitions:
  fast: "150ms ease-in-out"
  normal: "250ms ease-in-out"
  slow: "350ms ease-in-out"
  usage:
    fast: "Button hover, toggle, focus ring"
    normal: "Dropdown open, tab switch, toast appear"
    slow: "Sidebar collapse, modal open, page transition"

z_index:
  dropdown: "1000"
  sticky: "1020"
  modal_backdrop: "1040"
  modal: "1050"
  toast: "1060"
  tooltip: "1070"
```

### Component References

| Component Name | Angular Component | Variants | Screens Used |
|---------------|-------------------|----------|--------------|
| PrimaryButton | `app-button[variant="primary"]` | Default, Loading, Disabled | All screens |
| SecondaryButton | `app-button[variant="secondary"]` | Default, Hover, Disabled | SCR-006, SCR-007, SCR-014 |
| DestructiveButton | `app-button[variant="destructive"]` | Default, Confirm required | SCR-007, SCR-012, SCR-017 |
| IconButton | `app-icon-button` | Default, Hover, Active | SCR-013, SCR-026 |
| FAB | `app-fab` | Default | SCR-011, SCR-029 |
| TextInput | `app-input[type="text"]` | Default, Focus, Error, Disabled, ReadOnly | All forms |
| PasswordInput | `app-input[type="password"]` | Default, Show/Hide | SCR-002, SCR-003 |
| DatePicker | `app-date-picker` | Single, Range | SCR-004, SCR-007, SCR-021, SCR-023 |
| DropdownSelect | `app-select` | Single, Multi, Searchable | SCR-004, SCR-019, SCR-020 |
| ToggleSwitch | `app-toggle` | On, Off | SCR-005, SCR-009 |
| Checkbox | `app-checkbox` | Checked, Unchecked, Indeterminate | SCR-009, SCR-020 |
| FileUploadZone | `app-file-upload` | Idle, Dragging, Uploading, Error | SCR-011, SCR-028 |
| SearchInput | `app-search-input` | Default, Active, Results dropdown | SCR-018, SCR-020 |
| CodeEditor | `app-code-editor` | HTML mode, SMS mode | SCR-024 |
| SidebarNav | `app-sidebar` | Expanded, Collapsed | SCR-030 |
| HamburgerMenu | `app-mobile-nav` | Closed, Open drawer | SCR-030 |
| TabBar | `app-tab-bar` | Standard, Scrollable | SCR-014, SCR-019, SCR-024 |
| Breadcrumbs | `app-breadcrumbs` | Standard | SCR-030 |
| Pagination | `app-pagination` | Numbered, Prev/Next | SCR-007, SCR-012, SCR-020, SCR-021 |
| StepIndicator | `app-steps` | Horizontal, Active/Completed/Pending | SCR-001, SCR-003, SCR-027 |
| DataTable | `app-data-table` | Standard, Expandable, Card-on-mobile | SCR-007, SCR-012, SCR-020, SCR-021, SCR-025 |
| Card | `app-card` | Standard, Suggestion, Alert, KPI | SCR-006, SCR-008, SCR-016, SCR-017, SCR-023 |
| Timeline | `app-timeline` | Vertical filtered | SCR-015 |
| Badge | `app-badge` | Status, Role, Confidence, AI | SCR-014, SCR-017, SCR-025 |
| ProgressBar | `app-progress` | Linear, Circular | SCR-011, SCR-017, SCR-022 |
| SkeletonLoader | `app-skeleton` | Row, Card, Chart, Document | All screens |
| EmptyState | `app-empty-state` | Illustration + message + CTA | All screens |
| Chart | `app-chart` | Line, Bar, Donut | SCR-023 |
| Toast | `app-toast` | Success, Error, Info | All screens |
| ConfirmDialog | `app-confirm-dialog` | Standard, Destructive, Typed | SCR-007, SCR-012, SCR-016, SCR-017, SCR-020 |
| Banner | `app-banner` | Error retry, Connection lost, Warning | SCR-004, SCR-025 |
| AlertCard | `app-alert-card` | Critical, High, Moderate, Low | SCR-016 |
| SessionTimeoutModal | `app-session-timeout` | Warning with extend/logout | SCR-030 |
| Tooltip | `app-tooltip` | Standard, Rich | SCR-017, SCR-018 |
| LoadingSpinner | `app-spinner` | Button inline, Full-page | All screens |

### Accessibility Requirements

- **WCAG Level**: AA for all UI screens
- **Color Contrast**: Minimum 4.5:1 for normal text (14px), 3:1 for large text (18px+)
- **Focus States**: 2px solid primary-500 focus ring with 2px offset on all interactive elements
- **Keyboard Navigation**: Full tab order support, Enter/Space activation, Escape to close modals
- **Screen Reader**: ARIA roles, labels, live regions for dynamic content (queue updates, toasts, alerts)
- **Reduced Motion**: Respect `prefers-reduced-motion` media query, disable animations when set

### Responsive Behavior

```yaml
breakpoints:
  mobile:
    width: "375px"
    layout: "Single column"
    navigation: "Hamburger menu with slide-out drawer"
    tables: "Card-based layout"
    sidebar: "Hidden, accessible via hamburger"
    touch_targets: "Minimum 44x44px"

  tablet:
    width: "768px"
    layout: "Flexible columns (1-2)"
    navigation: "Collapsed sidebar (64px icons)"
    tables: "Horizontal scroll or responsive columns"

  desktop:
    width: "1440px"
    layout: "Multi-column (up to 4 for dashboard)"
    navigation: "Expanded sidebar (240px)"
    tables: "Full data table with all columns"
    content_max_width: "1200px with centered alignment"
```

### Implementation Scenarios

#### For New UI Components

```yaml
new_components:
  - name: "SuggestionCard"
    file_location: "components/suggestion-card/"
    design_specifications:
      width: "100%"
      padding: "24px"
      border_radius: "8px"
      border_left: "4px solid ai-main (#7E57C2)"
      background: "surface (#FFFFFF)"
      elevation: "level_1"
      states:
        - "default: neutral border-left"
        - "accepted: success border-left, green tint background"
        - "rejected: error border-left, strikethrough code text"
        - "modified: warning border-left, editable state"

  - name: "QueueStatusRow"
    file_location: "components/queue-status-row/"
    design_specifications:
      height: "56px"
      status_badge_width: "12px"
      status_colors:
        waiting: "#FF9800"
        in_progress: "#2196F3"
        completed: "#4CAF50"
        no_show: "#F44336"

  - name: "ConflictAlertCard"
    file_location: "components/conflict-alert-card/"
    design_specifications:
      width: "100%"
      padding: "16px 24px"
      border_left: "4px solid severity-color"
      severity_mapping:
        critical: "#C62828"
        high: "#E65100"
        moderate: "#FF9800"
        low: "#2196F3"
      acknowledge_button: "Required for critical severity"

  - name: "WaitlistCountdown"
    file_location: "components/waitlist-countdown/"
    design_specifications:
      timer_font: "JetBrains Mono, 20px, 600 weight"
      color_thresholds:
        normal: "#4CAF50 (>1 hour remaining)"
        warning: "#FF9800 (30min - 1 hour)"
        urgent: "#F44336 (<30 minutes)"
```

### Design Review Checklist

- [x] Design tokens extracted for all components
- [x] Component specifications documented
- [x] Visual validation criteria defined (5 states per screen)
- [x] Responsive behavior specified (3 breakpoints)
- [x] Accessibility requirements noted (WCAG 2.1 AA)
- [x] Color palette defined with semantic mapping
- [x] Typography scale defined with usage context
- [x] Spacing system defined with 4px base grid
- [x] Elevation system defined (3 levels)
- [x] AI content distinction pattern defined (purple accent)
- [x] Status color semantics defined and consistent
- [x] Screen-to-component mapping complete
