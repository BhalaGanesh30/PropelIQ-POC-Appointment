---
task_id: task_001
user_story: us_031
epic: EP-004
layer: Frontend
status: not-started
effort_hours: 7
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_031] Real-Time Queue Dashboard
- **Story Location**: [.propel/context/tasks/EP-004/us_031/us_031.md](.propel/context/tasks/EP-004/us_031/us_031.md)
- **Acceptance Criteria**:
  - AC-1: All today's appointments displayed with status badges (Waiting, In-Progress, Completed, No-Show) and wait-time estimates, rendered within 3 seconds.
  - AC-2: Queue dashboard refreshes the affected patient entry within 5 seconds on status change without a full page reload.
  - AC-3: Patient waiting longer than estimated wait time is highlighted with a visual warning indicator.
  - AC-4: Filter by appointment status shows only matching appointments.
- **Edge Cases**:
  - Edge Case 1: WebSocket/polling drop → reconnect attempt every 10s, "Reconnecting…" indicator shown, no stale data displayed.
  - Edge Case 2: 100+ patient queue → virtual scrolling applied; only visible DOM entries rendered; wait-time algorithm O(n) or better.

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-025-queue-dashboard.[html\|png\|jpg]` or provide external URL |
| **Screen Spec** | [figma_spec.md#SCR-025](.propel/context/docs/figma_spec.md#SCR-025) |
| **UXR Requirements** | UXR-106, UXR-201, UXR-202, UXR-203, UXR-301, UXR-303, UXR-404 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **PENDING**: UI-impacting task awaiting wireframe (provide file or URL before implementation)
>
> **Note**: SCR-025 is the Queue Dashboard screen (confirmed from figma_spec.md). User story referenced SCR-015 (Clinical Timeline) — corrected here.

### Screen States (SCR-025)

| State | Description |
|-------|-------------|
| Default | Queue table with status badges (color-coded), wait-time column, patient name, appointment type, action buttons |
| Loading | Skeleton rows (`app-skeleton`) during initial load and auto-refresh cycle |
| Empty | "No patients in queue" with walk-in CTA (`app-empty-state`) |
| Error | Connection lost banner (`app-banner`) with auto-reconnect countdown indicator |
| Validation | Check-in confirmation toast (`app-toast[success]`), status transition animation |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
| Reactive | RxJS | 7.x |
| Virtual Scroll | @angular/cdk/scrolling | 17.x |
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

Implement the `QueueDashboardComponent` Angular standalone component that renders the real-time staff queue for today's appointments. The component displays a full-width `app-data-table` with color-coded status badges, wait-time estimates, overdue row highlighting, and a status filter. It polls the backend `GET /api/v1/queue/today` endpoint every 15 seconds using an RxJS polling loop. For 100+ patient queues, `CdkVirtualScrollViewport` is applied to render only visible rows. A "Reconnecting…" `app-banner` appears if the poll request fails, and retries every 10 seconds. All 5 screen states from SCR-025 must be implemented.

---

## Dependent Tasks

- **task_002** — Queue API Endpoint must be deployed (or mocked via Angular interceptor) before this task can be validated end-to-end.
- **task_004** — DB Queue State Migration must be applied so the API returns `QueueState` fields.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `QueueDashboardComponent` | CREATE | New Angular standalone component |
| `QueuePollingService` | CREATE | RxJS polling service for queue data |
| `app/features/queue/` | CREATE | New feature module folder |
| `StaffShellModule` / routing | MODIFY | Register `/staff/queue` route |
| `app-data-table` | USE | Existing design-system component — no modification |
| `app-badge` | USE | Existing design-system component — no modification |
| `app-skeleton` | USE | Existing design-system component — no modification |
| `app-banner` | USE | Existing design-system component — no modification |
| `app-empty-state` | USE | Existing design-system component — no modification |
| `app-toast` | USE | Existing design-system component — no modification |
| `app-select` | USE | Existing design-system component — no modification |

---

## Implementation Plan

1. **Scaffold feature module**: Create `app/features/queue/` directory with `queue-dashboard.component.ts`, `queue-dashboard.component.html`, `queue-dashboard.component.scss`, and `queue-polling.service.ts`.
2. **Define data model**: Create `QueueEntry` interface matching `QueueEntryDto` from the backend (`patientId`, `patientName`, `appointmentType`, `status`, `arrivedAt`, `estimatedWaitMinutes`, `isOverdue`).
3. **Implement `QueuePollingService`**: Use `interval(15000).pipe(startWith(0), switchMap(() => http.get<QueueEntry[]>('/api/v1/queue/today')))` with `retry({ delay: 10000 })` and a `catchError` side-effect that sets `connectionError = true` signal.
4. **Implement status filter**: Use Angular signals (`signal<string>('ALL')`) and a computed derived list `filteredQueue = computed(() => ...)` — no external state manager required.
5. **Implement `app-data-table` binding**: Map `filteredQueue` to the table's `dataSource`, configure columns (`patient`, `type`, `status`, `waitTime`, `actions`). Apply `[class.overdue]` row class when `entry.isOverdue === true`.
6. **Implement virtual scrolling**: Wrap `app-data-table` inside `<cdk-virtual-scroll-viewport itemSize="72">` to cap DOM nodes for queues with 100+ entries.
7. **Implement screen states**: Bind `[ngIf]` / `@if` blocks to `loading`, `empty`, `connectionError`, and `entries` signals. Show `app-skeleton` rows (5) on initial load. Show `app-banner[variant="error"]` with "Reconnecting…" text when `connectionError = true`.
8. **Implement status badge color semantics**: Pass `status` to `app-badge` with variant map: `Waiting → warning`, `InProgress → info`, `Completed → success`, `NoShow → neutral`.

---

## Current Project State

```
app/
├── features/
│   ├── queue/                    ← CREATE (this task)
│   │   ├── queue-dashboard.component.ts
│   │   ├── queue-dashboard.component.html
│   │   ├── queue-dashboard.component.scss
│   │   └── queue-polling.service.ts
│   └── [existing features...]
├── shared/
│   └── models/
│       └── queue-entry.model.ts  ← CREATE
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks (task_002, task_004) are complete and the actual project structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/queue/queue-dashboard.component.ts` | Standalone Angular component — queue table, polling, filter, states |
| CREATE | `app/features/queue/queue-dashboard.component.html` | Template — data table, badges, filter, banners, empty/loading/error states |
| CREATE | `app/features/queue/queue-dashboard.component.scss` | Overdue row highlight (`.overdue { background: #FFF3E0; border-left: 4px solid #FF9800; }`), responsive layout |
| CREATE | `app/features/queue/queue-polling.service.ts` | RxJS polling service with `interval`, `retry`, error signal |
| CREATE | `app/shared/models/queue-entry.model.ts` | `QueueEntry` TypeScript interface |
| MODIFY | `app/app.routes.ts` (or staff routing module) | Add `{ path: 'staff/queue', component: QueueDashboardComponent }` |

---

## External References

- Angular CDK Virtual Scroll: https://material.angular.io/cdk/scrolling/overview (Angular 17 docs)
- RxJS `interval` + `switchMap` polling pattern: https://rxjs.dev/api/index/function/interval
- RxJS `retry` with delay config: https://rxjs.dev/api/operators/retry
- Angular Signals (`signal`, `computed`): https://angular.dev/guide/signals (Angular 17+)
- WCAG 2.2 AA color contrast checker (for overdue highlight): https://webaim.org/resources/contrastchecker/
- Angular `@if` control flow (Angular 17 new syntax): https://angular.dev/guide/templates/control-flow

---

## Build Commands

```bash
# Development server
ng serve

# Production build
ng build --configuration production

# Run Angular unit tests
ng test

# Lint
ng lint
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (component renders, filter logic, overdue highlighting)
- [ ] Integration tests pass — polling service connects to `GET /api/v1/queue/today` and refreshes table
- [ ] **[UI Task]** Visual comparison against SCR-025 wireframe when available at 375px, 768px, 1440px
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment (once wireframe uploaded)
- [ ] Polling resumes after a simulated network failure within 10 seconds
- [ ] Virtual scrolling renders only visible rows when queue has 100+ entries (DOM inspection)
- [ ] All 5 screen states visible (Default / Loading / Empty / Error / Validation)
- [ ] Status filter hides non-matching rows reactively without page reload
- [ ] Overdue rows display amber highlight and warning indicator (UXR-303)
- [ ] WCAG AA color contrast met for status badges (4.5:1 ratio minimum for badge text)
- [ ] ARIA live region applied to queue table for screen reader announcements (UXR-404)

---

## Implementation Checklist

- [ ] Scaffold `app/features/queue/` directory and create component, service, and model files
- [ ] Define `QueueEntry` TypeScript interface in `app/shared/models/queue-entry.model.ts`
- [ ] Implement `QueuePollingService` with `interval(15000)` + `retry({ delay: 10000 })` + `connectionError` signal
- [ ] Implement `QueueDashboardComponent` with `app-data-table` binding and computed `filteredQueue` signal
- [ ] Implement status badge color variant map (`Waiting → warning`, `InProgress → info`, `Completed → success`, `NoShow → neutral`)
- [ ] Implement overdue row CSS class (`.overdue`) with warning-amber left border and background tint
- [ ] Implement `CdkVirtualScrollViewport` wrapper for queues with 100+ entries
- [ ] Implement all 5 screen states: Default, Loading, Empty, Error, Validation
- [ ] Register `/staff/queue` route in staff routing module
- [ ] Add ARIA `role="status"` live region for auto-refresh announcements (screen reader support per UXR-404)
- [ ] **[UI Task — MANDATORY]** Reference SCR-025 wireframe from Design References table when wireframe becomes available
- [ ] **[UI Task — MANDATORY]** Validate UI matches wireframe at 375px, 768px, 1440px before marking task complete
