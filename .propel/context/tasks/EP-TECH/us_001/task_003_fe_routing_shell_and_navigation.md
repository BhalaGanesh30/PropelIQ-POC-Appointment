# Task - TASK_003

## Requirement Reference

- User Story: us_001
- Story Location: .propel/context/tasks/EP-TECH/us_001/us_001.md
- Acceptance Criteria:
  - AC-1: Given the project repository is cloned, When `npm install && ng serve` is run, Then the Angular 17 application compiles without errors and a routing shell renders at `http://localhost:4200`.
  - AC-3: Given the routing shell is active, When a user navigates to a defined lazy-loaded route, Then the correct module loads and renders within the shell without full page reload.
- Edge Case:
  - What happens when a user navigates to an undefined route? Wildcard route redirects to a 404 component.
  - How does the system handle navigation guard failures? Guard redirects to login with the attempted URL stored for post-auth redirect.

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
| Library | Angular Router | 17.x |
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

Create the application shell layout with Material toolbar and sidenav, configure the Angular router with lazy-loaded feature routes using standalone component patterns, implement a wildcard route that redirects to a 404 "Page Not Found" component, and create an authentication navigation guard (`CanActivate` functional guard) that redirects unauthenticated users to a login path while preserving the attempted URL for post-authentication redirect. This task delivers the routing foundation that all feature modules plug into.

## Dependent Tasks

- task_001_fe_angular_project_scaffold (requires Angular workspace and folder structure)
- task_002_fe_angular_material_setup (requires Material theme and components for shell layout)

## Impacted Components

- New: `app/src/app/layouts/main-layout/main-layout.component.ts` (shell with toolbar, sidenav, router-outlet)
- Modified: `app/src/app/app.routes.ts` (route definitions with lazy loading and wildcard)
- Modified: `app/src/app/app.component.ts` (render main layout)
- New: `app/src/app/features/dashboard/` (placeholder lazy-loaded feature)
- New: `app/src/app/features/not-found/not-found.component.ts` (404 page)
- New: `app/src/app/core/guards/auth.guard.ts` (functional CanActivate guard)
- New: `app/src/app/core/services/auth.service.ts` (authentication state stub)

## Implementation Plan

1. **Create the main shell layout component** (`MainLayoutComponent`) as a standalone component using Material `mat-toolbar`, `mat-sidenav-container`, and `<router-outlet>`. The toolbar displays the application title and the sidenav provides placeholder navigation links.
2. **Create placeholder feature module** under `features/dashboard/` with a standalone `DashboardComponent` and a `routes.ts` file exporting child routes as a default export for lazy loading.
3. **Create the 404 Not Found component** (`NotFoundComponent`) as a standalone component under `features/not-found/` displaying a user-friendly message with a "Return to Home" link.
4. **Configure route definitions** in `app.routes.ts`:
   - Default path `''` redirects to `/dashboard`
   - `dashboard` path uses `loadChildren` with dynamic import for lazy loading
   - `login` path loads a placeholder login component
   - Wildcard `**` path renders `NotFoundComponent`
5. **Create the auth guard** as a functional `CanActivateFn` guard in `core/guards/auth.guard.ts`. The guard checks an injected `AuthService` for authentication state. If unauthenticated, it stores the attempted URL in the router state and redirects to `/login`.
6. **Create auth service stub** in `core/services/auth.service.ts` with an `isAuthenticated()` method returning `true` by default (to be replaced in EP-001). Include `redirectUrl` property to store the attempted URL.
7. **Wire routing into app.config.ts** by adding `provideRouter(routes)` with `withComponentInputBinding()` for route parameter binding.
8. **Verify lazy loading** by confirming separate chunk files are generated in production build and navigation between routes does not trigger full page reloads.

### Angular 17 Lazy Loading Reference (Standalone Components)

```typescript
// app.routes.ts
export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadChildren: () => import('./features/dashboard/routes'),
    canActivate: [authGuard]
  },
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  { path: '**', component: NotFoundComponent }
];
```

```typescript
// features/dashboard/routes.ts
export default [
  { path: '', component: DashboardComponent }
] satisfies Route[];
```

Source: Angular 17.3.12 standalone components routing guide

### Functional Auth Guard Reference

```typescript
// core/guards/auth.guard.ts
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  authService.redirectUrl = state.url;
  return router.createUrlTree(['/login']);
};
```

Source: Angular 17.3.12 router guard documentation

## Current Project State

```text
app/
├── angular.json
├── package.json
├── tsconfig.json
├── src/
│   ├── main.ts
│   ├── index.html
│   ├── styles.scss          (Material theme applied)
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.prod.ts
│   └── app/
│       ├── app.component.ts
│       ├── app.config.ts    (provideAnimationsAsync configured)
│       ├── app.routes.ts    (empty routes placeholder)
│       ├── core/
│       ├── shared/
│       ├── features/
│       └── layouts/
```

> Assumes task_001 and task_002 are completed. Update on execution if structure differs.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | app/src/app/layouts/main-layout/main-layout.component.ts | Shell layout with Material toolbar, sidenav, and router-outlet |
| CREATE | app/src/app/layouts/main-layout/main-layout.component.html | Shell template with mat-toolbar, mat-sidenav-container, router-outlet |
| CREATE | app/src/app/layouts/main-layout/main-layout.component.scss | Shell layout styles for full-height viewport and sidenav width |
| CREATE | app/src/app/features/dashboard/dashboard.component.ts | Placeholder dashboard standalone component |
| CREATE | app/src/app/features/dashboard/routes.ts | Dashboard child routes with default export |
| CREATE | app/src/app/features/not-found/not-found.component.ts | 404 Page Not Found standalone component |
| CREATE | app/src/app/features/login/login.component.ts | Placeholder login standalone component for guard redirect target |
| CREATE | app/src/app/core/guards/auth.guard.ts | Functional CanActivateFn guard with redirect-to-login logic |
| CREATE | app/src/app/core/services/auth.service.ts | Auth service stub with isAuthenticated() and redirectUrl |
| MODIFY | app/src/app/app.routes.ts | Add lazy-loaded dashboard route, login route, wildcard 404 route |
| MODIFY | app/src/app/app.component.ts | Render MainLayoutComponent as the root view |
| MODIFY | app/src/app/app.config.ts | Add provideRouter(routes) with withComponentInputBinding() |

## External References

- Angular 17 standalone routing with loadChildren: https://github.com/angular/angular/blob/17.3.12/aio/content/guide/standalone-components.md
- Angular 17 wildcard routes and 404: https://github.com/angular/angular/blob/17.3.12/aio/content/guide/router.md
- Angular 17 functional route guards (CanActivateFn): https://angular.io/api/router/CanActivateFn
- Angular Material Sidenav: https://material.angular.io/components/sidenav/overview
- Angular Material Toolbar: https://material.angular.io/components/toolbar/overview
- Angular lazy loading with default exports: Uses `satisfies Route[]` pattern for type-safe default exports

## Build Commands

```bash
# Serve and test routing navigation
ng serve

# Verify lazy-loaded chunks in production build
ng build --configuration production

# Inspect output for separate chunk files (dashboard module)
```

## Implementation Validation Strategy

- [ ] `ng serve` compiles and the shell layout renders at `http://localhost:4200`
- [ ] Navigating to `/dashboard` lazy-loads the dashboard chunk (verify in browser DevTools Network tab)
- [ ] Navigating to `/nonexistent` renders the 404 Not Found component
- [ ] Auth guard redirects to `/login` when `AuthService.isAuthenticated()` returns `false`
- [ ] Auth guard stores the attempted URL in `AuthService.redirectUrl`
- [ ] `ng build --configuration production` generates separate chunk files for lazy-loaded routes
- [ ] No full page reloads occur during client-side navigation

## Implementation Checklist

- [ ] Create `MainLayoutComponent` standalone component in `layouts/main-layout/` with Material toolbar, sidenav, and `<router-outlet>`
- [ ] Create `DashboardComponent` standalone component in `features/dashboard/` with `routes.ts` default export
- [ ] Create `NotFoundComponent` standalone component in `features/not-found/` with user-friendly 404 message and home link
- [ ] Create placeholder `LoginComponent` standalone component in `features/login/`
- [ ] Create `authGuard` functional guard in `core/guards/auth.guard.ts` with redirect-to-login and URL preservation
- [ ] Create `AuthService` stub in `core/services/auth.service.ts` with `isAuthenticated()` and `redirectUrl` property
- [ ] Configure route definitions in `app.routes.ts` with lazy-loaded dashboard, login, and wildcard 404 routes
- [ ] Verify lazy-loaded chunks are generated and navigation does not trigger full page reloads
