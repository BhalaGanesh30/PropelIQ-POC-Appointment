# Task - TASK_002

## Requirement Reference

- User Story: us_001
- Story Location: .propel/context/tasks/EP-TECH/us_001/us_001.md
- Acceptance Criteria:
  - AC-2: Given the Angular workspace is initialized, When Angular Material is imported, Then the Material Design theme is applied globally and all Material components are available.
- Edge Case:
  - N/A (theming task; no user-facing navigation edge cases)

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | designsystem.md#colors, designsystem.md#typography |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | N/A | N/A |
| Database | N/A | N/A |
| Library | Angular Material | 17.x |
| Library | Angular CDK | 17.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Install Angular Material 17 and Angular CDK into the workspace, configure a custom Material Design theme using SCSS with project-specific color palettes and typography, apply the theme globally, set up WCAG 2.1 AA accessibility defaults (NFR-009), and verify that all Material components are available and themed correctly. This establishes the visual design foundation for all subsequent UI tasks.

## Dependent Tasks

- task_001_fe_angular_project_scaffold (requires initialized Angular 17 workspace)

## Impacted Components

- Modified: `app/package.json` (new Angular Material and CDK dependencies)
- Modified: `app/angular.json` (Material styles import)
- New: `app/src/styles.scss` (global theme definition and Material core styles)
- New: `app/src/app/core/material/` (Material module or provider configuration)
- Modified: `app/src/app/app.config.ts` (animations provider for Material)
- Modified: `app/src/index.html` (Material typography class, Roboto font, Material Icons)

## Implementation Plan

1. **Install Angular Material 17** using `ng add @angular/material` with the Indigo/Pink preset (or custom) and configure animations.
2. **Define custom SCSS theme** using Angular Material's `define-palette`, `define-light-theme`, and `define-typography-config` mixins. Configure primary, accent, and warn palettes aligned with designsystem.md tokens.
3. **Apply global theme** by including `mat.all-component-themes($theme)` in `src/styles.scss` and configuring `mat.core()` for baseline resets.
4. **Configure typography** using `mat.define-typography-config()` with project font families. Add Roboto font via Google Fonts CDN link in `index.html`.
5. **Add Material Icons** font reference in `index.html` for icon support across all components.
6. **Enable animations** by providing `provideAnimationsAsync()` in `app.config.ts` for Material component animations.
7. **Configure WCAG 2.1 AA accessibility** by enabling Material's strong focus indicators, ensuring color contrast ratios meet 4.5:1 minimum, and applying the `mat-typography` class to the document body (NFR-009).
8. **Verify integration** by adding a sample Material button and confirming theme application renders correctly.

### Angular Material 17 Theme Configuration Reference

```scss
@use '@angular/material' as mat;

@include mat.core();

$app-primary: mat.define-palette(mat.$indigo-palette, 500);
$app-accent: mat.define-palette(mat.$pink-palette, A200, A100, A400);
$app-typography: mat.define-typography-config();

$app-theme: mat.define-light-theme((
  color: (
    primary: $app-primary,
    accent: $app-accent,
  ),
  typography: $app-typography,
  density: 0,
));

@include mat.all-component-themes($app-theme);
```

Source: Angular Material 17.3.10 theming guide

## Current Project State

```text
app/
├── angular.json
├── package.json
├── tsconfig.json
├── tsconfig.app.json
├── src/
│   ├── main.ts
│   ├── index.html
│   ├── styles.scss
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.prod.ts
│   └── app/
│       ├── app.component.ts
│       ├── app.config.ts
│       ├── app.routes.ts
│       ├── core/
│       ├── shared/
│       ├── features/
│       └── layouts/
```

> Assumes task_001 is completed. Update on execution if structure differs.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | app/package.json | Add @angular/material and @angular/cdk 17.x dependencies |
| MODIFY | app/angular.json | Add Material prebuilt styles reference if using hybrid approach |
| MODIFY | app/src/styles.scss | Define custom Material theme with palettes, typography, and global resets |
| MODIFY | app/src/index.html | Add Roboto font link, Material Icons font link, and mat-typography body class |
| MODIFY | app/src/app/app.config.ts | Add provideAnimationsAsync() to application providers |
| CREATE | app/src/app/shared/material/ | Shared directory for Material re-exports if needed |

## External References

- Angular Material theming (v17): https://github.com/angular/components/blob/17.3.10/guides/theming.md
- Angular Material typography (v17): https://github.com/angular/components/blob/17.3.10/guides/typography.md
- Angular Material getting started: https://material.angular.io/guide/getting-started
- Angular Material custom theme SCSS: `mat.define-palette()`, `mat.define-light-theme()`, `mat.all-component-themes()`
- WCAG 2.1 AA color contrast: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html
- Google Fonts Roboto: https://fonts.google.com/specimen/Roboto

## Build Commands

```bash
# Add Angular Material
ng add @angular/material

# Verify build after Material integration
ng serve

# Production build verification
ng build --configuration production
```

## Implementation Validation Strategy

- [ ] `ng serve` compiles without errors after Material installation
- [ ] Material Design theme is visually applied (primary color renders on Material components)
- [ ] `ng build --configuration production` succeeds with Material included
- [ ] Roboto font and Material Icons load correctly in browser
- [ ] Body element has `mat-typography` class for global typography
- [ ] Color contrast meets WCAG 2.1 AA 4.5:1 minimum ratio (NFR-009)

## Implementation Checklist

- [ ] Run `ng add @angular/material` to install Angular Material 17.x and Angular CDK 17.x
- [ ] Define custom SCSS theme in `src/styles.scss` using `mat.define-palette()`, `mat.define-light-theme()`, and `mat.all-component-themes()`
- [ ] Add `@include mat.core()` for baseline Material resets in `src/styles.scss`
- [ ] Add Roboto font and Material Icons CDN links to `src/index.html`
- [ ] Add `class="mat-typography"` to `<body>` element in `src/index.html`
- [ ] Add `provideAnimationsAsync()` to providers in `app.config.ts`
- [ ] Verify WCAG 2.1 AA color contrast for primary and accent palettes (NFR-009)
- [ ] Confirm `ng build --configuration production` passes with Material integration
