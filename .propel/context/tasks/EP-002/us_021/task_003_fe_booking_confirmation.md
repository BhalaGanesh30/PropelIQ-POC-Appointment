# Task - TASK_003

## Requirement Reference

- User Story: us_021
- Story Location: .propel/context/tasks/EP-002/us_021/us_021.md
- Acceptance Criteria:
  - AC-1: Given I confirm my appointment selection with completed intake, When the booking request is submitted, Then the slot is atomically reserved, the appointment record is persisted, and I receive a confirmation within 1 minute.
  - AC-2: Given the booking is confirmed, When the confirmation email is sent, Then it contains a PDF appointment summary, a scannable QR code uniquely identifying the appointment, and an ICS calendar file attachment.
  - AC-3: Given I access the confirmation page, When I click "Download PDF," Then the PDF downloads immediately containing appointment date, time, duration, type, and provider name.
  - AC-4: Given two patients attempt to book the same slot simultaneously, When the concurrent requests are processed, Then only one booking succeeds using optimistic concurrency control; the second patient receives "Slot no longer available" and is offered the next available slot.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-006-booking-confirmation.html` (pending) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-006 |
| **UXR Requirements** | UXR-105 (PDF/QR/ICS in single view), UXR-201 (typography), UXR-202 (spacing), UXR-301 (responsive breakpoints), UXR-501 (loading spinner on submit) |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-008 but the actual Booking Confirmation screen is **SCR-006** per figma_spec.md. SCR-008 is "Waitlist View."

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

Implement the Booking Confirmation page (SCR-006) that presents the post-booking summary with three downloadable artifacts: PDF summary, QR code, and ICS calendar file (AC-2, AC-3, UXR-105). The page consumes the booking response from the `POST /api/v1/bookings` call (task_001) and displays the confirmation card with appointment details, a green success banner (SCR-006 Validation state), and an action button row for Download PDF, QR Code, and Add to Calendar. A "Confirm Booking" submit button on the intake form flow triggers the booking API call with a loading spinner and double-submit prevention (UXR-501). If a concurrent conflict occurs (AC-4), the page shows "Slot no longer available" with a link to the suggested next slot. The page supports 3 responsive breakpoints (UXR-301): mobile 375px, tablet 768px, desktop 1440px. The confirmation card is a centered single-column layout (max-width 600px per SCR-006 specification). Navigation includes a link to the appointment list after confirmation.

## Dependent Tasks

- US_021 task_001 (requires `POST /api/v1/bookings` API with `BookingResponse` and `SlotConflictResponse`)
- US_021 task_002 (requires `GET /api/v1/bookings/{id}/artifacts/{type}` download endpoint)
- US_020 task_003 (requires intake form completion flow that navigates to booking confirmation)

## Impacted Components

- New: `client/src/app/features/booking/booking-confirmation.component.ts` (standalone component, confirmation page)
- New: `client/src/app/features/booking/booking-confirmation.component.html` (template)
- New: `client/src/app/features/booking/booking-confirmation.component.scss` (styles)
- New: `client/src/app/features/booking/booking-api.service.ts` (API service for booking + artifact download)
- New: `client/src/app/features/booking/models/booking.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/booking/slot-conflict-dialog.component.ts` (dialog for AC-4 conflict)
- Modify: `client/src/app/app.routes.ts` (add booking confirmation route)

## Implementation Plan

1. **Create TypeScript interfaces** for booking API models:

```typescript
// client/src/app/features/booking/models/booking.models.ts

export interface CreateBookingRequest {
  slotId: string;
  intakeRecordId: string;
}

export interface BookingResponse {
  appointmentId: string;
  confirmationCode: string;
  appointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
  location: string | null;
  status: string;
  bookedAt: string;
}

export interface SlotConflictResponse {
  message: string;
  nextAvailableSlotId: string | null;
  nextAvailableTime: string | null;
}

export type ArtifactType = 'pdf' | 'qr' | 'ics';
```

2. **Create `BookingApiService`**:

```typescript
// client/src/app/features/booking/booking-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateBookingRequest,
  BookingResponse,
  ArtifactType
} from './models/booking.models';

@Injectable({ providedIn: 'root' })
export class BookingApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/bookings';

  createBooking(request: CreateBookingRequest): Observable<BookingResponse> {
    return this.http.post<BookingResponse>(this.baseUrl, request);
  }

  getBooking(id: string): Observable<BookingResponse> {
    return this.http.get<BookingResponse>(`${this.baseUrl}/${id}`);
  }

  downloadArtifact(appointmentId: string, type: ArtifactType): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl}/${appointmentId}/artifacts/${type}`,
      { responseType: 'blob' }
    );
  }
}
```

3. **Create `SlotConflictDialogComponent`** for AC-4 concurrent conflict:

```typescript
// client/src/app/features/booking/slot-conflict-dialog.component.ts
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { SlotConflictResponse } from './models/booking.models';

@Component({
  selector: 'app-slot-conflict-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>
      <mat-icon color="warn">event_busy</mat-icon>
      Slot No Longer Available
    </h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
      @if (data.nextAvailableTime) {
        <p class="next-slot-suggestion">
          Next available slot:
          <strong>{{ data.nextAvailableTime | date:'EEEE, MMM d, y h:mm a' }}</strong>
        </p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      @if (data.nextAvailableSlotId) {
        <button mat-flat-button color="primary"
                [mat-dialog-close]="data.nextAvailableSlotId">
          Book Next Slot
        </button>
      }
      <button mat-stroked-button (click)="dialogRef.close('search')">
        Search Again
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .next-slot-suggestion {
      margin-top: 16px;
      padding: 12px;
      background: var(--mat-sys-surface-variant);
      border-radius: 8px;
    }
  `]
})
export class SlotConflictDialogComponent {
  readonly data = inject<SlotConflictResponse>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<SlotConflictDialogComponent>);
}
```

4. **Create `BookingConfirmationComponent`**:

```typescript
// client/src/app/features/booking/booking-confirmation.component.ts
import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { BookingApiService } from './booking-api.service';
import { BookingResponse, ArtifactType } from './models/booking.models';
import { SlotConflictDialogComponent } from './slot-conflict-dialog.component';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-booking-confirmation',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './booking-confirmation.component.html',
  styleUrl: './booking-confirmation.component.scss'
})
export class BookingConfirmationComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly bookingApi = inject(BookingApiService);
  private readonly dialog = inject(MatDialog);

  // State signals
  readonly booking = signal<BookingResponse | null>(null);
  readonly isSubmitting = signal(false);
  readonly isLoading = signal(true);
  readonly downloadingArtifact = signal<ArtifactType | null>(null);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    // Check if navigated with booking response in state (from intake submit)
    const state = history.state as { booking?: BookingResponse };
    if (state?.booking) {
      this.booking.set(state.booking);
      this.isLoading.set(false);
      return;
    }

    // Otherwise load by appointmentId from route param
    const appointmentId = this.route.snapshot.paramMap.get('appointmentId');
    if (appointmentId) {
      this.loadBooking(appointmentId);
    }
  }

  /** AC-1: Submit booking from intake flow (called from intake page) */
  submitBooking(slotId: string, intakeRecordId: string): void {
    if (this.isSubmitting()) return; // UXR-501: prevent double-submit

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.bookingApi.createBooking({ slotId, intakeRecordId }).subscribe({
      next: (response) => {
        this.booking.set(response);
        this.isSubmitting.set(false);
        this.isLoading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmitting.set(false);

        if (error.status === 409) {
          // AC-4: Slot conflict — show dialog with next available
          this.handleSlotConflict(error.error);
        } else {
          this.errorMessage.set(
            'Failed to create booking. Please try again.'
          );
        }
      }
    });
  }

  /** AC-3: Download artifact (PDF, QR, ICS) */
  downloadArtifact(type: ArtifactType): void {
    const appointmentId = this.booking()?.appointmentId;
    if (!appointmentId || this.downloadingArtifact()) return;

    this.downloadingArtifact.set(type);

    this.bookingApi.downloadArtifact(appointmentId, type).subscribe({
      next: (blob) => {
        const fileNames: Record<ArtifactType, string> = {
          pdf: 'confirmation.pdf',
          qr: 'qrcode.png',
          ics: 'appointment.ics'
        };

        // Trigger browser download
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileNames[type];
        anchor.click();
        URL.revokeObjectURL(url);

        this.downloadingArtifact.set(null);
      },
      error: () => {
        this.downloadingArtifact.set(null);
        this.errorMessage.set(
          'Artifact not yet available. Please try again shortly.'
        );
      }
    });
  }

  navigateToAppointments(): void {
    this.router.navigate(['/appointments']);
  }

  private loadBooking(appointmentId: string): void {
    this.bookingApi.getBooking(appointmentId).subscribe({
      next: (response) => {
        this.booking.set(response);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Could not load booking details.');
      }
    });
  }

  private handleSlotConflict(conflict: unknown): void {
    const dialogRef = this.dialog.open(SlotConflictDialogComponent, {
      data: conflict,
      width: '480px'
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result === 'search') {
        this.router.navigate(['/appointments/search']);
      } else if (result) {
        // Navigate to rebook with suggested slot
        this.router.navigate(['/appointments/search'], {
          queryParams: { slotId: result }
        });
      }
    });
  }
}
```

5. **Create template**:

```html
<!-- client/src/app/features/booking/booking-confirmation.component.html -->

<!-- Loading state (SCR-006) -->
@if (isLoading()) {
  <div class="confirmation-container">
    <mat-spinner diameter="48"></mat-spinner>
    <p class="loading-text">Loading confirmation...</p>
  </div>
}

<!-- Submitting state (UXR-501: spinner during booking) -->
@if (isSubmitting()) {
  <div class="confirmation-container">
    <mat-spinner diameter="48"></mat-spinner>
    <p class="loading-text">Confirming your booking...</p>
  </div>
}

<!-- Confirmation card (SCR-006 Default + Validation states) -->
@if (booking(); as b) {
  <div class="confirmation-container">

    <!-- Success banner (SCR-006 Validation state) -->
    <div class="success-banner" role="alert">
      <mat-icon>check_circle</mat-icon>
      <span>Appointment Confirmed!</span>
    </div>

    <!-- Confirmation card (max-width 600px per SCR-006 layout) -->
    <mat-card class="confirmation-card">
      <mat-card-header>
        <mat-card-title>Appointment Details</mat-card-title>
        <mat-card-subtitle>
          Confirmation Code: <strong>{{ b.confirmationCode }}</strong>
        </mat-card-subtitle>
      </mat-card-header>

      <mat-card-content>
        <div class="detail-grid">
          <div class="detail-row">
            <span class="detail-label">
              <mat-icon>calendar_today</mat-icon> Date
            </span>
            <span class="detail-value">
              {{ b.appointmentTime | date:'EEEE, MMMM d, y' }}
            </span>
          </div>

          <div class="detail-row">
            <span class="detail-label">
              <mat-icon>schedule</mat-icon> Time
            </span>
            <span class="detail-value">
              {{ b.appointmentTime | date:'h:mm a' }}
            </span>
          </div>

          <div class="detail-row">
            <span class="detail-label">
              <mat-icon>timelapse</mat-icon> Duration
            </span>
            <span class="detail-value">
              {{ b.durationMinutes }} minutes
            </span>
          </div>

          <div class="detail-row">
            <span class="detail-label">
              <mat-icon>medical_services</mat-icon> Type
            </span>
            <span class="detail-value">{{ b.appointmentType }}</span>
          </div>

          <div class="detail-row">
            <span class="detail-label">
              <mat-icon>person</mat-icon> Provider
            </span>
            <span class="detail-value">
              {{ b.providerName ?? 'TBD' }}
            </span>
          </div>

          @if (b.location) {
            <div class="detail-row">
              <span class="detail-label">
                <mat-icon>location_on</mat-icon> Location
              </span>
              <span class="detail-value">{{ b.location }}</span>
            </div>
          }
        </div>
      </mat-card-content>

      <!-- Action buttons (UXR-105: PDF, QR, ICS in single view) -->
      <mat-card-actions class="action-row">
        <button mat-flat-button color="primary"
                (click)="downloadArtifact('pdf')"
                [disabled]="downloadingArtifact() !== null"
                aria-label="Download PDF summary">
          @if (downloadingArtifact() === 'pdf') {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            <mat-icon>picture_as_pdf</mat-icon>
          }
          Download PDF
        </button>

        <button mat-stroked-button
                (click)="downloadArtifact('qr')"
                [disabled]="downloadingArtifact() !== null"
                aria-label="Download QR code">
          @if (downloadingArtifact() === 'qr') {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            <mat-icon>qr_code</mat-icon>
          }
          QR Code
        </button>

        <button mat-stroked-button
                (click)="downloadArtifact('ics')"
                [disabled]="downloadingArtifact() !== null"
                aria-label="Add to calendar">
          @if (downloadingArtifact() === 'ics') {
            <mat-spinner diameter="20"></mat-spinner>
          } @else {
            <mat-icon>event</mat-icon>
          }
          Add to Calendar
        </button>
      </mat-card-actions>
    </mat-card>

    <!-- Navigation -->
    <div class="navigation-row">
      <button mat-button
              (click)="navigateToAppointments()"
              aria-label="View all appointments">
        <mat-icon>arrow_back</mat-icon>
        View My Appointments
      </button>
    </div>
  </div>
}

<!-- Error state (SCR-006 Error state) -->
@if (errorMessage(); as msg) {
  <div class="confirmation-container">
    <mat-card class="error-card">
      <mat-card-content>
        <mat-icon color="warn">error_outline</mat-icon>
        <p>{{ msg }}</p>
        <button mat-flat-button color="primary"
                (click)="errorMessage.set(null)">
          Try Again
        </button>
      </mat-card-content>
    </mat-card>
  </div>
}
```

6. **Create styles** with responsive breakpoints:

```scss
// client/src/app/features/booking/booking-confirmation.component.scss

.confirmation-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 24px 16px;
  min-height: 60vh;
}

.loading-text {
  margin-top: 16px;
  color: var(--mat-sys-on-surface-variant);
}

// SCR-006 Validation state: green success banner
.success-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 24px;
  background: var(--mat-sys-tertiary-container);
  color: var(--mat-sys-on-tertiary-container);
  border-radius: 8px;
  margin-bottom: 24px;
  width: 100%;
  max-width: 600px;

  mat-icon {
    color: #2e7d32;
    font-size: 28px;
    height: 28px;
    width: 28px;
  }

  span {
    font-size: 18px;
    font-weight: 500;
  }
}

// SCR-006 layout: single-column centered card (max-width 600px)
.confirmation-card {
  width: 100%;
  max-width: 600px;
}

.detail-grid {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px 0;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--mat-sys-outline-variant);

  &:last-child {
    border-bottom: none;
  }
}

.detail-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
  color: var(--mat-sys-on-surface-variant);

  mat-icon {
    font-size: 20px;
    height: 20px;
    width: 20px;
  }
}

.detail-value {
  font-weight: 400;
  text-align: right;
}

// UXR-105: PDF, QR, ICS buttons in single row
.action-row {
  display: flex;
  gap: 12px;
  padding: 16px;
  justify-content: center;
  flex-wrap: wrap;

  button {
    display: flex;
    align-items: center;
    gap: 6px;
  }
}

.navigation-row {
  margin-top: 24px;
}

.error-card {
  max-width: 600px;
  text-align: center;

  mat-icon {
    font-size: 48px;
    height: 48px;
    width: 48px;
    margin-bottom: 16px;
  }
}

// UXR-301: Responsive breakpoints
// Mobile (375px)
@media (max-width: 599px) {
  .confirmation-container {
    padding: 16px 12px;
  }

  .success-banner {
    padding: 8px 16px;

    span {
      font-size: 16px;
    }
  }

  .detail-row {
    flex-direction: column;
    align-items: flex-start;
    gap: 4px;
  }

  .detail-value {
    text-align: left;
    padding-left: 28px;
  }

  .action-row {
    flex-direction: column;

    button {
      width: 100%;
    }
  }
}

// Tablet (768px)
@media (min-width: 600px) and (max-width: 1023px) {
  .confirmation-container {
    padding: 24px;
  }
}

// Desktop (1440px)
@media (min-width: 1024px) {
  .confirmation-container {
    padding: 32px;
  }
}
```

7. **Add route** to application routing:

```typescript
// Add to client/src/app/app.routes.ts
{
  path: 'booking/confirmation/:appointmentId',
  loadComponent: () =>
    import('./features/booking/booking-confirmation.component')
      .then(m => m.BookingConfirmationComponent),
  canActivate: [authGuard]
},
{
  path: 'booking/confirmation',
  loadComponent: () =>
    import('./features/booking/booking-confirmation.component')
      .then(m => m.BookingConfirmationComponent),
  canActivate: [authGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── features/
            │   ├── scheduling/                    (existing from US_019)
            │   ├── intake/                        (existing from US_020)
            │   └── booking/                       (new module)
            │       ├── booking-confirmation.component.ts
            │       ├── booking-confirmation.component.html
            │       ├── booking-confirmation.component.scss
            │       ├── booking-api.service.ts
            │       ├── slot-conflict-dialog.component.ts
            │       └── models/
            │           └── booking.models.ts
            └── app.routes.ts                      (modify — add booking routes)
```

> Placeholder: Update on execution based on US_021 task_001 and task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/booking/models/booking.models.ts | TypeScript interfaces for booking request, response, conflict, artifact types |
| CREATE | client/src/app/features/booking/booking-api.service.ts | HttpClient service for createBooking, getBooking, downloadArtifact |
| CREATE | client/src/app/features/booking/slot-conflict-dialog.component.ts | Material dialog for AC-4 concurrent conflict with next-slot suggestion |
| CREATE | client/src/app/features/booking/booking-confirmation.component.ts | Standalone component with signals for booking state, submit, download |
| CREATE | client/src/app/features/booking/booking-confirmation.component.html | Template with success banner, detail card, action buttons, responsive layout |
| CREATE | client/src/app/features/booking/booking-confirmation.component.scss | Responsive styles for 375px, 768px, 1440px breakpoints, 600px max-width card |
| MODIFY | client/src/app/app.routes.ts | Add lazy-loaded booking confirmation routes with auth guard |

## External References

- Angular Material Card: https://material.angular.io/components/card/overview
- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- Angular Signals: https://angular.dev/guide/signals
- Blob download pattern: https://developer.mozilla.org/en-US/docs/Web/API/URL/createObjectURL

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Navigate to: http://localhost:4200/booking/confirmation/<appointmentId>
# Or: complete intake form → submit booking → auto-navigate to confirmation
```

## Implementation Validation Strategy

- [ ] Confirmation page displays appointment date, time, duration, type, provider name (AC-2, AC-3)
- [ ] Green success banner with check icon renders on confirmation (SCR-006 Validation state)
- [ ] "Download PDF" triggers blob download of `confirmation.pdf` (AC-3)
- [ ] "QR Code" triggers blob download of `qrcode.png` (AC-2)
- [ ] "Add to Calendar" triggers blob download of `appointment.ics` (AC-2)
- [ ] All three download actions available in a single summary view (UXR-105)
- [ ] Loading spinner on submit button during booking API call (UXR-501)
- [ ] Double-submit prevention via `isSubmitting` signal guard (UXR-501)
- [ ] HTTP 409 conflict triggers dialog with "Slot no longer available" and next-slot suggestion (AC-4)
- [ ] Confirmation card max-width 600px, centered single-column layout (SCR-006 layout)
- [ ] Responsive at mobile 375px (stacked detail rows, full-width buttons), tablet 768px, desktop 1440px (UXR-301)
- [ ] Download buttons disabled during any active download to prevent overlap
- [ ] `aria-label` attributes on all interactive elements for WCAG 2.1 AA (NFR-009)

## Implementation Checklist

- [x] Create `BookingResponse`, `SlotConflictResponse`, `CreateBookingRequest` TypeScript interfaces
- [x] Create `BookingApiService` with `createBooking`, `getBooking`, `downloadArtifact` methods
- [x] Create `SlotConflictDialogComponent` for HTTP 409 conflict handling with next-slot suggestion
- [x] Create `BookingConfirmationComponent` with signals for loading, submitting, downloading states
- [x] Create template with success banner, detail grid, and PDF/QR/ICS action button row
- [x] Create SCSS with 600px max-width card and responsive breakpoints (375px, 768px, 1440px)
- [x] Add lazy-loaded booking routes to `scheduling.routes.ts` (under existing auth-guarded `scheduling` path)
- [x] Verify `aria-label` on all buttons and WCAG 2.1 AA compliance
