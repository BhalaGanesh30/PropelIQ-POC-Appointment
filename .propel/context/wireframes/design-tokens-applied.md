# Design Tokens Applied - Unified Patient Access and Clinical Intelligence Platform

## Token Application Summary

This wireframe set applies the platform token model from
`.propel/context/docs/designsystem.md` directly to page shells, form patterns,
data-heavy operational views, and AI-assisted decision surfaces.

## Color Mapping

| Token Group | Usage in Wireframes |
|-------------|---------------------|
| Primary 500 `#1976D2` | Primary actions, active navigation, key chart accents, focus treatments |
| Secondary 500 `#26A69A` | Secondary actions, verified badges, supportive highlight rails |
| Neutral scale | Surface layers, dividers, body text, inactive controls, table structure |
| Semantic success | Confirmation banners, verified states, completed workflow markers |
| Semantic warning | Waitlist urgency, advisory banners, expiring tasks |
| Semantic error | Inline validation, destructive actions, critical alerts |
| Semantic info | Informational banners, helper states, low-severity notifications |
| AI main `#7E57C2` | AI badges, confidence bars, AI-prefilled field tinting |

## Typography Mapping

| Token | Applied To |
|-------|------------|
| Display 32/40 700 | Authentication and shell hero headers |
| Heading 1 24/32 600 | Page titles and modal headings |
| Heading 2 20/28 600 | Card titles, section titles, tab titles |
| Subtitle 16/24 500 | Table headers, KPI labels, form sections |
| Body 14/20 400 | Body copy, input text, table cells |
| Caption 12/16 400 | Helper text, metadata, timestamps, badge labels |
| Code 13/20 mono | ICD-10/CPT codes, template editor, audit payload snippets |

## Spacing and Radius Rules

| Token | Usage |
|-------|-------|
| `spacing.sm` 8px | Tight inline chip gaps, icon-label spacing |
| `spacing.md` 16px | Form row spacing, toolbar gaps, table cell padding |
| `spacing.lg` 24px | Card padding, page block separation |
| `spacing.xl` 32px | Section separation, shell content padding |
| `radius.sm` 4px | Badges, tags, pills |
| `radius.md` 8px | Buttons, inputs, cards |
| `radius.lg` 12px | Dialogs, large containers, split panes |

## Elevation and Motion

| Token | Applied To |
|-------|------------|
| Level 1 | Cards, dropdowns, badges on elevated surfaces |
| Level 2 | Modals, side panels, fixed action bars |
| Level 3 | Drawer shell and persistent sidebar emphasis |
| Fast 150ms | Button hover, focus ring, toggle switch |
| Normal 250ms | Tab changes, toast appearance, dropdown open |
| Slow 350ms | Sidebar collapse, modal entry, route transition cue |

## Accessibility Guardrails Applied

- All color uses assume WCAG AA contrast thresholds from the Figma spec.
- Semantic states are never color-only; each has label or icon reinforcement.
- Focus treatment uses the primary palette with sufficient contrast against both
  white and neutral surfaces.
- Mobile controls preserve 44x44 minimum touch targets.

## AI-Specific Styling Decisions

| Pattern | Styling |
|---------|---------|
| AI badge | AI main background with light tint and explicit `AI-assisted` text |
| AI-prefilled fields | Soft AI light background with badge chip and editable affordance |
| Confidence meter | AI-colored progress bar with numeric percentage |
| Explainability panel | Neutral surface with AI-accent border and source link row |

## Screen Groups and Token Emphasis

| Screen Group | Token Emphasis |
|-------------|----------------|
| Authentication | Display typography, primary actions, calm neutral backgrounds |
| Patient booking | Primary workflow buttons, success/validation states, sticky footer emphasis |
| Document management | Neutral data surfaces, upload progress semantics, viewer split-pane elevation |
| Clinical intelligence | AI accent, severity colors, source traceability cues |
| Administration | Dense neutral shell, chart accents, configuration feedback banners |