---
task_id: task_001
user_story: us_036
epic: EP-004
layer: Frontend
status: not-started
effort_hours: 8
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_036] Daily Schedule Calendar with Drag-and-Drop
- **Story Location**: [.propel/context/tasks/EP-004/us_036/us_036.md](.propel/context/tasks/EP-004/us_036/us_036.md)
- **Acceptance Criteria**:
  - AC-1: All appointments for the selected date are displayed in a time-grid calendar layout sorted by appointment time.
  - AC-2: Dragging an appointment block to a different time slot reschedules it; the override reason dialog is shown and the change is reflected in the queue immediately.
  - AC-3: Print action renders a print-optimized layout with all appointments, patient names, types, and durations formatted for A4/Letter paper.
  - AC-4: Selecting a different date via the calendar date picker updates the schedule view within 1 second.
- **Edge Cases**:
  - Edge Case 1: Drag-and-drop results in a time conflict; system highlights the conflict and cancels the drop with a conflict resolution dialog.
  - Edge Case 2: Day with no appointments; empty time-grid with message "No appointments scheduled for this date."

---

## Design References (Frontend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A (Figma project not yet linked) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | [.propel/context/wireframes/Hi-Fi/wireframe-SCR-026-daily-schedule-view.html](.propel/context/wireframes/Hi-Fi/wireframe-SCR-026-daily-schedule-view.html) |
| **Screen Spec** | [figma_spec.md#SCR-026](.propel/context/docs/figma_spec.md#SCR-026) |
| **UXR Requirements** | UXR-110, UXR-201, UXR-202, UXR-301, UXR-304 |
| **Design Tokens** | [designsystem.md#colors](.propel/context/docs/designsystem.md), [designsystem.md#typography](.propel/context/docs/designsystem.md), [designsystem.md#spacing](.propel/context/docs/designsystem.md) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local wireframe file exists at specified path.
>
> **Note**: US_036 references SCR-019 (System Configuration, EP-011). The daily schedule calendar is SCR-026 per figma_spec.md, which is the dedicated Daily Schedule View screen under EP-004.

### Screen States (SCR-026)

| State | Description |
|-------|-------------|
| Default | Time-grid calendar (7 AM - 7 PM) with appointment blocks colour-coded by type, date picker, print button |
| Loading | Skeleton time-grid during data fetch |
| Empty | "No appointments scheduled for this date" message on empty time-grid |
| Error | Load failure banner with retry button |
| Validation | Drag feedback (ghost block, valid/invalid drop zone highlights), drop confirmation toast, print preview modal |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| UI Library | Angular Material + CDK | 17.x |
| Drag and Drop | Angular CDK DragDrop | 17.x |
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

Implement the `DailyScheduleComponent` as a standalone Angular 17 component for SCR-026. The component renders a vertical time-grid calendar spanning 7 AM to 7 PM with 15-minute interval rows on desktop. Each appointment is rendered as a positioned block within the grid, colour-coded by appointment type (Scheduled, Walk-In, Override) and sized proportionally to its duration (15/30/60 minutes). The component uses Angular CDK `cdkDrag` and `cdkDropList` directives for drag-and-drop rearrangement. On drag start, a ghost block previews the appointment at the cursor (UXR-110). On drop over a valid time slot, the component opens the `OverrideReasonDialogComponent` (from US_034/task_001) to capture a mandatory reason, then calls `PUT /api/v1/schedule/reschedule` to persist the change. On drop over an occupied time slot, a conflict resolution dialog appears and the drop is cancelled. A date picker in the header loads appointments for the selected date via `GET /api/v1/schedule/daily?date={yyyy-MM-dd}`. A print button triggers `window.print()` with a `@media print` stylesheet that renders a clean A4/Letter-formatted layout. The empty state shows "No appointments scheduled for this date" on the grid.

---

## Dependent Tasks

- **us_036/task_002** — `GET /api/v1/schedule/daily` and `PUT /api/v1/schedule/reschedule` endpoints must be deployed (or mocked via Angular HTTP interceptor).
- **us_031/task_001** — `QueueDashboardComponent` must exist; drag-and-drop rescheduling updates queue state.
- **us_034/task_001** — `OverrideReasonDialogComponent` must exist; reused for drag-drop override reason capture.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `DailyScheduleComponent` | CREATE | Standalone Angular 17 component — time-grid, appointment blocks, DnD, print |
| `DailyScheduleComponent` (template) | CREATE | Time-grid rows, `cdkDrag` appointment blocks, date picker, print button |
| `DailyScheduleComponent` (styles) | CREATE | Time-grid layout, appointment block colours, drag ghost, `@media print` stylesheet |
| `DailyScheduleService` | CREATE | Angular service wrapping `GET /api/v1/schedule/daily` and `PUT /api/v1/schedule/reschedule` |
| `ScheduleAppointment` model | CREATE | TypeScript interface: `appointmentId`, `patientName`, `appointmentType`, `startTime`, `duration`, `status` |
| `RescheduleRequest` model | CREATE | TypeScript interface: `appointmentId`, `newStartTime`, `overrideReason` |
| Staff routing module | MODIFY | Register `/staff/schedule` route pointing to `DailyScheduleComponent` |

---

## Implementation Plan

1. **Create `DailyScheduleService`** in `app/features/schedule/daily-schedule.service.ts`: method `getSchedule(date: string): Observable<ScheduleAppointment[]>` wrapping `HttpClient.get('/api/v1/schedule/daily', { params: { date } })`. Method `reschedule(payload: RescheduleRequest): Observable<void>` wrapping `HttpClient.put('/api/v1/schedule/reschedule', payload)`.
2. **Create time-grid structure**: Generate an array of 15-minute interval rows from 07:00 to 19:00 (48 rows). Each row has a time label and a drop zone. Use `@for` to render rows. Each row is a `cdkDropList` that accepts dragged appointment blocks.
3. **Render appointment blocks**: For each appointment, calculate the grid row start (based on `startTime`) and row span (based on `duration / 15`). Use CSS `grid-row` positioning. Colour-code blocks by `appointmentType` using CSS classes: `type-scheduled` (primary), `type-walkin` (accent), `type-override` (warning). Display patient name, appointment type, and duration text inside each block.
4. **Implement drag-and-drop** using Angular CDK:
   - Apply `cdkDrag` directive to each appointment block.
   - Apply `cdkDragPreview` for the ghost block (UXR-110: visual feedback on drag start).
   - On `cdkDragDrop` event, calculate the new target time from the drop list index. Check if the target slot is occupied. If occupied, show `app-banner[variant="error"]` conflict message and cancel the drop. If free, open `OverrideReasonDialogComponent` (from US_034). On dialog confirmation, call `dailyScheduleService.reschedule({ appointmentId, newStartTime, overrideReason })`. On success, update the local appointment array and show `app-toast[success]` "Appointment rescheduled."
5. **Implement date picker**: Use Angular Material `mat-datepicker` in the header. On date selection, call `dailyScheduleService.getSchedule(selectedDate)`. Show skeleton loader during fetch. Target sub-1-second load per AC-4 (backed by Redis-cached API).
6. **Implement empty state**: When the schedule array is empty, render the time-grid with an overlay message: "No appointments scheduled for this date" using `app-empty-state` component.
7. **Implement print layout**: Add a Print `app-icon-button` in the header. Create a `@media print` stylesheet in the component SCSS: hide sidebar, header, date picker, and drag handles; render appointment blocks as a flat table with patient name, type, start time, duration columns; set page size to A4/Letter with appropriate margins. Use `window.print()` on button click.
8. **Register route**: Add `{ path: 'staff/schedule', component: DailyScheduleComponent }` to the staff routing module. Add navigation link in the staff dashboard sidebar.

---

## Current Project State

```
app/
├── features/
│   ├── schedule/                                     ← CREATE (this task)
│   │   ├── daily-schedule.component.ts
│   │   ├── daily-schedule.component.html
│   │   ├── daily-schedule.component.scss
│   │   └── daily-schedule.service.ts
│   ├── scheduling/                                   ← EXISTS (us_034)
│   │   ├── override-reason-dialog.component.*       ← REUSE
│   │   └── ...
│   ├── queue/                                        ← EXISTS (us_031)
│   │   └── ...
│   └── [existing feature modules...]
├── shared/
│   └── models/
│       ├── schedule-appointment.model.ts             ← CREATE
│       └── reschedule-request.model.ts               ← CREATE
└── [existing app structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual feature folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `app/features/schedule/daily-schedule.component.ts` | Standalone component: time-grid, CDK DnD, date picker, print, empty state |
| CREATE | `app/features/schedule/daily-schedule.component.html` | Template: time-grid rows with `cdkDropList`, appointment blocks with `cdkDrag`, header with date picker and print button |
| CREATE | `app/features/schedule/daily-schedule.component.scss` | Time-grid layout, appointment block colours, drag ghost styles, `@media print` stylesheet |
| CREATE | `app/features/schedule/daily-schedule.service.ts` | Service wrapping daily schedule GET and reschedule PUT endpoints |
| CREATE | `app/shared/models/schedule-appointment.model.ts` | `ScheduleAppointment` interface |
| CREATE | `app/shared/models/reschedule-request.model.ts` | `RescheduleRequest` interface |
| MODIFY | `app/app.routes.ts` (or staff routing) | Add `{ path: 'staff/schedule', component: DailyScheduleComponent }` |

---

## External References

- Angular CDK Drag and Drop: https://material.angular.io/cdk/drag-drop/overview
- Angular CDK `cdkDragPreview` for custom drag previews: https://material.angular.io/cdk/drag-drop/overview#customizing-the-drag-preview
- Angular Material Datepicker: https://material.angular.io/components/datepicker/overview
- CSS Grid for time-grid layout: https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_grid_layout
- `@media print` stylesheet best practices: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/print
- UXR-110: Drag-and-drop MUST provide visual feedback on drag start, hover target, and drop confirmation
- FR-SO-006: Daily schedule views with drag-and-drop rearrangement and print-friendly rendering
- SCR-026 wireframe: `.propel/context/wireframes/Hi-Fi/wireframe-SCR-026-daily-schedule-view.html`

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

- [ ] Unit tests pass for `DailyScheduleComponent` (time-grid rendering, drag-drop events, date selection, empty state)
- [ ] Unit tests pass for `DailyScheduleService` (HTTP calls mocked)
- [ ] **[UI Task]** Visual comparison against wireframe at 375px, 768px, 1440px
- [ ] **[UI Task]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Drag ghost preview visible on drag start (UXR-110)
- [ ] Conflict detection cancels drop and shows resolution dialog
- [ ] Override reason dialog opens on valid drop; reschedule persisted on confirmation
- [ ] Print button produces A4/Letter-formatted layout with all appointment details
- [ ] Date picker loads schedule within 1 second (AC-4)
- [ ] Empty state renders correctly for days with no appointments

---

## Implementation Checklist

- [ ] Create `DailyScheduleService` wrapping `GET /api/v1/schedule/daily` and `PUT /api/v1/schedule/reschedule`
- [ ] Implement time-grid layout (7 AM - 7 PM, 15-min intervals) with CSS Grid and appointment block positioning
- [ ] Implement Angular CDK drag-and-drop with `cdkDrag`, `cdkDropList`, and `cdkDragPreview` ghost block (UXR-110)
- [ ] Integrate `OverrideReasonDialogComponent` from US_034 on valid drop; call reschedule API on confirmation
- [ ] Implement conflict detection: cancel drop and show conflict dialog when target slot is occupied
- [ ] Implement date picker with `mat-datepicker`; load schedule on date change with skeleton loader
- [ ] Implement `@media print` stylesheet for A4/Letter-formatted print layout with appointment table
- [ ] **[UI Task - MANDATORY]** Reference wireframe from Design References table during implementation
