# Task - TASK_002

## Requirement Reference

- User Story: us_024
- Story Location: .propel/context/tasks/EP-002/us_024/us_024.md
- Acceptance Criteria:
  - AC-1: Given I have a confirmed appointment, When I click "Add to Calendar" on the confirmation page or in the confirmation email, Then an ICS file is generated containing the appointment title, date, time, duration, and location.
  - AC-3: Given my appointment is rescheduled, When I export the updated ICS, Then the ICS contains the updated date and time with a `SEQUENCE` increment so calendar apps recognize it as an update.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-006-booking-confirmation.html` (pending) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-006 |
| **UXR Requirements** | UXR-105 (PDF download, QR code, and ICS export in single summary view), UXR-201 (typography), UXR-301 (responsive breakpoints) |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-008 but the actual Booking Confirmation screen is **SCR-006** per figma_spec.md (consistent off-by-2 mismatch). SCR-008 is Waitlist View.

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Frontend | Angular Material | 17.x |
| Frontend | RxJS | 7.x |
| Frontend | TypeScript | 5.x |
| Backend | N/A | N/A |
| Database | N/A | N/A |
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

Add an "Add to Calendar" download action to the Appointment History page (SCR-007) so patients can export or re-export ICS files for confirmed and rescheduled appointments. The booking confirmation page (SCR-006, from US_021 task_003) already provides ICS download for new bookings per UXR-105, but patients need to access the updated ICS after a reschedule (AC-3) or re-download the original ICS at any time (AC-1). This task adds a `mat-icon-button` with a calendar icon to each appointment card/row on the Appointment History page, calling the existing `GET /api/v1/bookings/{id}/artifacts/ics` endpoint (from US_021 task_002). The button is enabled only for `Confirmed` and `Rescheduled` status appointments and hidden for `Cancelled` entries. A loading spinner replaces the icon during download, and a snackbar confirms the download or reports errors. The button includes `aria-label` for accessibility (NFR-009) and renders correctly at all 3 responsive breakpoints (UXR-301).

## Dependent Tasks

- US_024 task_001 (requires enhanced ICS with SEQUENCE and TZID)
- US_022 task_002 (requires Appointment History page with appointment cards/table)
- US_021 task_003 (requires BookingApiService with artifact download pattern)
- US_021 task_002 (requires GET /api/v1/bookings/{id}/artifacts/{type} endpoint)

## Impacted Components

- Modify: `client/src/app/features/appointments/appointment-history.component.ts` (add calendar download method)
- Modify: `client/src/app/features/appointments/appointment-history.component.html` (add "Add to Calendar" button to cards/rows)
- Modify: `client/src/app/features/appointments/appointment-history.component.scss` (button positioning styles)
- Reuse: `client/src/app/features/booking/booking-api.service.ts` (existing `downloadArtifact` method)

## Implementation Plan

1. **Add ICS download method to `AppointmentHistoryComponent`**:

```typescript
// client/src/app/features/appointments/appointment-history.component.ts
// Add to existing component

import { BookingApiService } from '../booking/booking-api.service';

// Add to component class
private readonly bookingApi = inject(BookingApiService);
readonly downloadingId = signal<string | null>(null);

downloadCalendarEvent(appointment: AppointmentEntry): void {
  if (this.downloadingId()) return;

  this.downloadingId.set(appointment.id);

  this.bookingApi.downloadArtifact(appointment.id, 'ics').subscribe({
    next: (blob) => {
      this.downloadingId.set(null);

      // Trigger browser download
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `appointment-${appointment.confirmationCode}.ics`;
      anchor.click();
      URL.revokeObjectURL(url);

      this.snackBar.open(
        'Calendar event downloaded.', 'Close',
        { duration: 3000 }
      );
    },
    error: () => {
      this.downloadingId.set(null);
      this.snackBar.open(
        'Failed to download calendar event.', 'Close',
        { duration: 5000 }
      );
    }
  });
}

canDownloadIcs(appointment: AppointmentEntry): boolean {
  return appointment.status === 'Confirmed'
      || appointment.status === 'Rescheduled';
}
```

2. **Add "Add to Calendar" button to desktop table row**:

```html
<!-- In appointment-history.component.html -->
<!-- Add to the actions column of the mat-table (desktop) -->

<!-- Add to Calendar button -->
@if (canDownloadIcs(appointment)) {
  <button mat-icon-button
          color="primary"
          [disabled]="downloadingId() !== null"
          (click)="downloadCalendarEvent(appointment)"
          [attr.aria-label]="'Add ' + appointment.appointmentType
            + ' appointment to calendar'">
    @if (downloadingId() === appointment.id) {
      <mat-spinner diameter="20"></mat-spinner>
    } @else {
      <mat-icon>event</mat-icon>
    }
  </button>
}
```

3. **Add "Add to Calendar" button to mobile card view**:

```html
<!-- In appointment-history.component.html -->
<!-- Add to the mat-card-actions section (mobile) -->

@if (canDownloadIcs(appointment)) {
  <button mat-stroked-button
          [disabled]="downloadingId() !== null"
          (click)="downloadCalendarEvent(appointment)"
          [attr.aria-label]="'Add ' + appointment.appointmentType
            + ' appointment to calendar'">
    @if (downloadingId() === appointment.id) {
      <mat-spinner diameter="16"></mat-spinner>
    } @else {
      <mat-icon>event</mat-icon>
    }
    Add to Calendar
  </button>
}
```

4. **Add styles for calendar button positioning**:

```scss
// client/src/app/features/appointments/appointment-history.component.scss
// Add to existing styles

// Desktop table action cell
.action-cell {
  display: flex;
  gap: 4px;
  align-items: center;
}

// Mobile card calendar button
mat-card-actions {
  .calendar-btn {
    mat-icon {
      font-size: 18px;
      height: 18px;
      width: 18px;
      margin-right: 4px;
    }
  }
}

// UXR-301: Responsive breakpoints
@media (max-width: 599px) {
  .calendar-btn {
    width: 100%;
    justify-content: center;
  }
}
```

5. **Verify `BookingApiService.downloadArtifact` exists** (from US_021 task_003):

```typescript
// client/src/app/features/booking/booking-api.service.ts
// Existing method — reuse directly
downloadArtifact(bookingId: string, type: string): Observable<Blob> {
  return this.http.get(
    `${this.baseUrl}/${bookingId}/artifacts/${type}`,
    { responseType: 'blob' }
  );
}
```

No changes needed to `BookingApiService` — the existing `downloadArtifact` method already supports ICS download by passing `type: 'ics'`.

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── features/
            │   ├── appointments/                  (existing from US_022 task_002)
            │   │   ├── appointment-history.component.ts    (modify)
            │   │   ├── appointment-history.component.html  (modify)
            │   │   └── appointment-history.component.scss  (modify)
            │   ├── booking/                        (existing from US_021)
            │   │   ├── booking-api.service.ts      (reuse — downloadArtifact)
            │   │   └── booking-confirmation.component.ts  (existing — ICS button)
            │   ├── scheduling/                     (existing from US_019)
            │   ├── intake/                         (existing from US_020)
            │   └── waitlist/                       (existing from US_023)
            └── app.routes.ts                       (no changes)
```

> Placeholder: Update on execution based on US_022 task_002 and US_021 task_003 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | client/src/app/features/appointments/appointment-history.component.ts | Add `downloadCalendarEvent` method, `downloadingId` signal, `canDownloadIcs` check |
| MODIFY | client/src/app/features/appointments/appointment-history.component.html | Add "Add to Calendar" icon-button to table rows and card actions |
| MODIFY | client/src/app/features/appointments/appointment-history.component.scss | Add calendar button positioning and responsive styles |

## External References

- Angular Material Icon Button: https://material.angular.io/components/button/overview
- Blob Download Pattern: https://developer.mozilla.org/en-US/docs/Web/API/Blob

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Navigate to: http://localhost:4200/appointments/history
# Verify "Add to Calendar" button on confirmed/rescheduled appointments
```

## Implementation Validation Strategy

- [ ] "Add to Calendar" button visible on Confirmed appointment rows/cards (AC-1)
- [ ] "Add to Calendar" button visible on Rescheduled appointment rows/cards (AC-3)
- [ ] "Add to Calendar" button hidden on Cancelled appointments
- [ ] Clicking button triggers ICS file download with correct filename
- [ ] Loading spinner displays during download
- [ ] Snackbar confirms successful download
- [ ] Error snackbar displays on download failure
- [ ] `aria-label` includes appointment type for screen readers (NFR-009)
- [ ] Button renders correctly at 375px, 768px, 1440px breakpoints (UXR-301)

## Implementation Checklist

- [x] Add `downloadCalendarEvent` method using existing `BookingApiService.downloadArtifact`
- [x] Add `canDownloadIcs` guard for Confirmed and Rescheduled status only
- [x] Add calendar icon-button to desktop mat-table action column
- [x] Add calendar stroked-button to mobile card actions
- [x] Add loading spinner and download-in-progress guard
- [x] Add responsive styles for calendar button at 3 breakpoints
