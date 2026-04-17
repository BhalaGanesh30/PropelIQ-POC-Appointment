# Task - TASK_002

## Requirement Reference

- User Story: us_022
- Story Location: .propel/context/tasks/EP-002/us_022/us_022.md
- Acceptance Criteria:
  - AC-1: Given I have a confirmed appointment more than 24 hours away, When I submit a cancellation request, Then the appointment status is updated to cancelled, the slot is released, and I receive a cancellation confirmation email.
  - AC-2: Given I have a confirmed appointment more than 24 hours away, When I reschedule to a new available slot, Then the original slot is released, the new slot is atomically reserved, and an updated confirmation is sent.
  - AC-3: Given my appointment is within 24 hours, When I attempt to cancel or reschedule, Then the system displays "Changes not allowed within 24 hours of appointment" and the action is blocked.
  - AC-4: Given a staff member with override privileges, When they reschedule or cancel an appointment within the 24-hour window, Then the action is allowed with mandatory reason capture and an audit entry is created.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-007-appointment-history.html` (pending) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-007 |
| **UXR Requirements** | UXR-111 (confirmation dialog for destructive actions), UXR-201 (typography), UXR-202 (spacing), UXR-301 (responsive breakpoints), UXR-501 (loading spinner on submit) |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-009 but the actual screen containing reschedule/cancel actions is **SCR-007** (Appointment History) per figma_spec.md. SCR-009 is "Notification Preferences."

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

Implement the frontend reschedule and cancel interactions on the Appointment History page (SCR-007). The appointment table action column includes "Reschedule" and "Cancel" buttons for confirmed appointments. Clicking "Cancel" triggers a confirmation dialog per UXR-111 (destructive action pattern). For staff users, the dialog includes a mandatory reason textarea when the appointment is within 24 hours (AC-4). Clicking "Reschedule" navigates to the slot search page with context to select a new slot, then calls the reschedule API (AC-2). When a patient attempts to modify an appointment within 24 hours, the UI disables the action buttons and shows a tooltip "Changes not allowed within 24 hours of appointment" (AC-3). On successful cancel, a success toast appears and the row updates to "Cancelled" status. On successful reschedule, the row updates with the new appointment time. The page supports 3 responsive breakpoints (UXR-301): mobile 375px (card list), tablet 768px, desktop 1440px (full table). Submit buttons show loading spinners during API calls (UXR-501).

## Dependent Tasks

- US_022 task_001 (requires POST cancel and POST reschedule API endpoints)
- US_021 task_003 (requires BookingApiService and booking models from confirmation page)
- US_019 task_002 (requires slot search page for reschedule destination)

## Impacted Components

- New: `client/src/app/features/appointments/appointment-history.component.ts` (standalone component)
- New: `client/src/app/features/appointments/appointment-history.component.html` (template)
- New: `client/src/app/features/appointments/appointment-history.component.scss` (responsive styles)
- New: `client/src/app/features/appointments/cancel-dialog.component.ts` (UXR-111 confirmation dialog)
- New: `client/src/app/features/appointments/appointment-api.service.ts` (cancel/reschedule API calls)
- New: `client/src/app/features/appointments/models/appointment.models.ts` (TypeScript interfaces)
- Modify: `client/src/app/app.routes.ts` (add appointment history route)

## Implementation Plan

1. **Create TypeScript interfaces**:

```typescript
// client/src/app/features/appointments/models/appointment.models.ts

export interface AppointmentListItem {
  id: string;
  confirmationCode: string;
  appointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
  location: string | null;
  status: 'Confirmed' | 'Cancelled' | 'Completed' | 'NoShow';
  bookedAt: string;
  canModify: boolean; // computed: status === 'Confirmed' && >24h away
}

export interface CancelRequest {
  overrideReason?: string;
}

export interface CancelResponse {
  appointmentId: string;
  status: string;
  cancelledAt: string;
}

export interface RescheduleRequest {
  newSlotId: string;
  overrideReason?: string;
}

export interface RescheduleResponse {
  appointmentId: string;
  confirmationCode: string;
  newAppointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
  status: string;
  rescheduledAt: string;
}

export interface AppointmentFilter {
  startDate?: string;
  endDate?: string;
  status?: string;
  page: number;
  pageSize: number;
}
```

2. **Create `AppointmentApiService`**:

```typescript
// client/src/app/features/appointments/appointment-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AppointmentListItem,
  AppointmentFilter,
  CancelRequest,
  CancelResponse,
  RescheduleRequest,
  RescheduleResponse
} from './models/appointment.models';

interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class AppointmentApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/bookings';

  getAppointments(
    filter: AppointmentFilter
  ): Observable<PaginatedResponse<AppointmentListItem>> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.startDate)
      params = params.set('startDate', filter.startDate);
    if (filter.endDate)
      params = params.set('endDate', filter.endDate);
    if (filter.status)
      params = params.set('status', filter.status);

    return this.http.get<PaginatedResponse<AppointmentListItem>>(
      this.baseUrl, { params }
    );
  }

  cancelAppointment(
    id: string, request: CancelRequest
  ): Observable<CancelResponse> {
    return this.http.post<CancelResponse>(
      `${this.baseUrl}/${id}/cancel`, request
    );
  }

  rescheduleAppointment(
    id: string, request: RescheduleRequest
  ): Observable<RescheduleResponse> {
    return this.http.post<RescheduleResponse>(
      `${this.baseUrl}/${id}/reschedule`, request
    );
  }
}
```

3. **Create `CancelDialogComponent`** (UXR-111: confirmation dialog for destructive action):

```typescript
// client/src/app/features/appointments/cancel-dialog.component.ts
import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  MatDialogModule,
  MAT_DIALOG_DATA,
  MatDialogRef
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface CancelDialogData {
  appointmentTime: string;
  appointmentType: string;
  isWithin24Hours: boolean;
  isStaff: boolean;
}

export interface CancelDialogResult {
  confirmed: boolean;
  overrideReason?: string;
}

@Component({
  selector: 'app-cancel-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon color="warn">warning</mat-icon>
      Cancel Appointment
    </h2>
    <mat-dialog-content>
      <p>Are you sure you want to cancel your
        <strong>{{ data.appointmentType }}</strong> appointment on
        <strong>{{ data.appointmentTime | date:'EEEE, MMM d, y h:mm a' }}</strong>?
      </p>
      <p class="warning-text">This action cannot be undone.</p>

      <!-- AC-4: Staff override reason (required within 24h) -->
      @if (requiresReason()) {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Override Reason (Required)</mat-label>
          <textarea matInput
                    [(ngModel)]="overrideReason"
                    rows="3"
                    maxlength="1000"
                    placeholder="Explain why this cancellation is needed within 24 hours"
                    required
                    aria-label="Override reason for cancellation within 24 hours">
          </textarea>
          <mat-hint align="end">
            {{ overrideReason.length }} / 1000
          </mat-hint>
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close
              aria-label="Keep appointment">
        Keep Appointment
      </button>
      <button mat-flat-button color="warn"
              [disabled]="!canConfirm()"
              (click)="confirm()"
              aria-label="Confirm cancellation">
        <mat-icon>cancel</mat-icon>
        Confirm Cancellation
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .warning-text {
      color: var(--mat-sys-error);
      font-weight: 500;
      margin-top: 8px;
    }
    .full-width {
      width: 100%;
      margin-top: 16px;
    }
  `]
})
export class CancelDialogComponent {
  readonly data = inject<CancelDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(
    MatDialogRef<CancelDialogComponent>
  );

  overrideReason = '';

  readonly requiresReason = computed(
    () => this.data.isWithin24Hours && this.data.isStaff
  );

  readonly canConfirm = computed(() => {
    if (this.requiresReason()) {
      return this.overrideReason.trim().length > 0;
    }
    return true;
  });

  confirm(): void {
    const result: CancelDialogResult = {
      confirmed: true,
      overrideReason: this.requiresReason()
        ? this.overrideReason.trim()
        : undefined
    };
    this.dialogRef.close(result);
  }
}
```

4. **Create `AppointmentHistoryComponent`**:

```typescript
// client/src/app/features/appointments/appointment-history.component.ts
import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { AppointmentApiService } from './appointment-api.service';
import {
  AppointmentListItem,
  AppointmentFilter
} from './models/appointment.models';
import {
  CancelDialogComponent,
  CancelDialogData,
  CancelDialogResult
} from './cancel-dialog.component';

@Component({
  selector: 'app-appointment-history',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatDatepickerModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatSnackBarModule,
    MatCardModule,
    MatChipsModule
  ],
  templateUrl: './appointment-history.component.html',
  styleUrl: './appointment-history.component.scss'
})
export class AppointmentHistoryComponent implements OnInit {
  private readonly appointmentApi = inject(AppointmentApiService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  // State signals
  readonly appointments = signal<AppointmentListItem[]>([]);
  readonly totalCount = signal(0);
  readonly isLoading = signal(true);
  readonly actionInProgress = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  // Filter state
  readonly statusFilter = signal<string>('');
  readonly startDate = signal<string>('');
  readonly endDate = signal<string>('');
  readonly page = signal(0);
  readonly pageSize = signal(10);

  // Staff detection (from auth service)
  readonly isStaff = signal(false); // Populated from auth context

  // Table columns
  readonly displayedColumns = [
    'appointmentTime', 'appointmentType', 'providerName',
    'duration', 'status', 'actions'
  ];

  ngOnInit(): void {
    this.loadAppointments();
  }

  loadAppointments(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    const filter: AppointmentFilter = {
      page: this.page() + 1, // API is 1-indexed
      pageSize: this.pageSize(),
      status: this.statusFilter() || undefined,
      startDate: this.startDate() || undefined,
      endDate: this.endDate() || undefined
    };

    this.appointmentApi.getAppointments(filter).subscribe({
      next: (response) => {
        this.appointments.set(response.items);
        this.totalCount.set(response.totalCount);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Failed to load appointments.');
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.page.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadAppointments();
  }

  onFilterChange(): void {
    this.page.set(0);
    this.loadAppointments();
  }

  /** Check if appointment can be modified by current user */
  canModify(apt: AppointmentListItem): boolean {
    if (apt.status !== 'Confirmed') return false;
    if (this.isStaff()) return true; // Staff can always act (AC-4)
    return this.isMoreThan24HoursAway(apt);
  }

  /** AC-3: Check 24h window */
  isMoreThan24HoursAway(apt: AppointmentListItem): boolean {
    const appointmentTime = new Date(apt.appointmentTime).getTime();
    const now = Date.now();
    const twentyFourHours = 24 * 60 * 60 * 1000;
    return (appointmentTime - now) > twentyFourHours;
  }

  /** Tooltip for disabled actions (AC-3) */
  getActionTooltip(apt: AppointmentListItem): string {
    if (apt.status !== 'Confirmed')
      return 'Only confirmed appointments can be modified';
    if (!this.isMoreThan24HoursAway(apt) && !this.isStaff())
      return 'Changes not allowed within 24 hours of appointment';
    return '';
  }

  /** AC-1: Cancel appointment with UXR-111 confirmation dialog */
  cancelAppointment(apt: AppointmentListItem): void {
    if (this.actionInProgress()) return;

    const isWithin24h = !this.isMoreThan24HoursAway(apt);

    const dialogData: CancelDialogData = {
      appointmentTime: apt.appointmentTime,
      appointmentType: apt.appointmentType,
      isWithin24Hours: isWithin24h,
      isStaff: this.isStaff()
    };

    const dialogRef = this.dialog.open(CancelDialogComponent, {
      data: dialogData,
      width: '480px',
      disableClose: true
    });

    dialogRef.afterClosed().subscribe((result: CancelDialogResult) => {
      if (!result?.confirmed) return;

      this.actionInProgress.set(apt.id);

      this.appointmentApi.cancelAppointment(apt.id, {
        overrideReason: result.overrideReason
      }).subscribe({
        next: () => {
          this.actionInProgress.set(null);
          this.snackBar.open(
            'Appointment cancelled successfully', 'Close',
            { duration: 4000 }
          );
          // Update row in-place
          this.appointments.update(list =>
            list.map(a => a.id === apt.id
              ? { ...a, status: 'Cancelled' as const, canModify: false }
              : a
            )
          );
        },
        error: (err) => {
          this.actionInProgress.set(null);
          const message = err.error?.message
            ?? 'Failed to cancel appointment.';
          this.snackBar.open(message, 'Close', { duration: 5000 });
        }
      });
    });
  }

  /** AC-2: Reschedule — navigate to slot search with context */
  rescheduleAppointment(apt: AppointmentListItem): void {
    this.router.navigate(['/appointments/search'], {
      queryParams: {
        rescheduleFrom: apt.id,
        appointmentType: apt.appointmentType,
        duration: apt.durationMinutes
      }
    });
  }

  retryLoad(): void {
    this.loadAppointments();
  }
}
```

5. **Create template**:

```html
<!-- client/src/app/features/appointments/appointment-history.component.html -->

<div class="history-container">
  <h1>My Appointments</h1>

  <!-- Filter bar (SCR-007 Default state) -->
  <div class="filter-bar">
    <mat-form-field appearance="outline">
      <mat-label>Status</mat-label>
      <mat-select [(ngModel)]="statusFilter"
                  (selectionChange)="onFilterChange()"
                  aria-label="Filter by status">
        <mat-option value="">All</mat-option>
        <mat-option value="Confirmed">Confirmed</mat-option>
        <mat-option value="Cancelled">Cancelled</mat-option>
        <mat-option value="Completed">Completed</mat-option>
        <mat-option value="NoShow">No Show</mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Start Date</mat-label>
      <input matInput
             [matDatepicker]="startPicker"
             [(ngModel)]="startDate"
             (dateChange)="onFilterChange()"
             aria-label="Filter start date">
      <mat-datepicker-toggle matIconSuffix [for]="startPicker">
      </mat-datepicker-toggle>
      <mat-datepicker #startPicker></mat-datepicker>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>End Date</mat-label>
      <input matInput
             [matDatepicker]="endPicker"
             [(ngModel)]="endDate"
             (dateChange)="onFilterChange()"
             aria-label="Filter end date">
      <mat-datepicker-toggle matIconSuffix [for]="endPicker">
      </mat-datepicker-toggle>
      <mat-datepicker #endPicker></mat-datepicker>
    </mat-form-field>
  </div>

  <!-- Loading state (SCR-007 Loading) -->
  @if (isLoading()) {
    <div class="skeleton-table">
      @for (row of [1, 2, 3, 4, 5]; track row) {
        <div class="skeleton-row"></div>
      }
    </div>
  }

  <!-- Error state (SCR-007 Error) -->
  @if (errorMessage(); as msg) {
    <div class="error-banner" role="alert">
      <mat-icon color="warn">error_outline</mat-icon>
      <span>{{ msg }}</span>
      <button mat-stroked-button (click)="retryLoad()"
              aria-label="Retry loading appointments">
        Retry
      </button>
    </div>
  }

  <!-- Empty state (SCR-007 Empty) -->
  @if (!isLoading() && !errorMessage() && appointments().length === 0) {
    <div class="empty-state">
      <mat-icon class="empty-icon">event_busy</mat-icon>
      <p>No appointments found</p>
      <button mat-flat-button color="primary"
              routerLink="/appointments/search"
              aria-label="Book new appointment">
        Book an Appointment
      </button>
    </div>
  }

  <!-- Desktop table view -->
  @if (!isLoading() && appointments().length > 0) {
    <div class="table-wrapper desktop-only">
      <table mat-table [dataSource]="appointments()">

        <ng-container matColumnDef="appointmentTime">
          <th mat-header-cell *matHeaderCellDef>Date & Time</th>
          <td mat-cell *matCellDef="let apt">
            {{ apt.appointmentTime | date:'MMM d, y h:mm a' }}
          </td>
        </ng-container>

        <ng-container matColumnDef="appointmentType">
          <th mat-header-cell *matHeaderCellDef>Type</th>
          <td mat-cell *matCellDef="let apt">{{ apt.appointmentType }}</td>
        </ng-container>

        <ng-container matColumnDef="providerName">
          <th mat-header-cell *matHeaderCellDef>Provider</th>
          <td mat-cell *matCellDef="let apt">
            {{ apt.providerName ?? 'TBD' }}
          </td>
        </ng-container>

        <ng-container matColumnDef="duration">
          <th mat-header-cell *matHeaderCellDef>Duration</th>
          <td mat-cell *matCellDef="let apt">
            {{ apt.durationMinutes }} min
          </td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Status</th>
          <td mat-cell *matCellDef="let apt">
            <mat-chip [class]="'status-' + apt.status.toLowerCase()">
              {{ apt.status }}
            </mat-chip>
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef>Actions</th>
          <td mat-cell *matCellDef="let apt">
            <div class="action-buttons">
              <!-- Reschedule button -->
              <button mat-icon-button
                      [disabled]="!canModify(apt) || actionInProgress() !== null"
                      [matTooltip]="getActionTooltip(apt) || 'Reschedule'"
                      (click)="rescheduleAppointment(apt)"
                      aria-label="Reschedule appointment">
                <mat-icon>edit_calendar</mat-icon>
              </button>

              <!-- Cancel button (UXR-111: destructive with confirmation) -->
              <button mat-icon-button color="warn"
                      [disabled]="!canModify(apt) || actionInProgress() !== null"
                      [matTooltip]="getActionTooltip(apt) || 'Cancel'"
                      (click)="cancelAppointment(apt)"
                      aria-label="Cancel appointment">
                @if (actionInProgress() === apt.id) {
                  <mat-spinner diameter="20"></mat-spinner>
                } @else {
                  <mat-icon>cancel</mat-icon>
                }
              </button>
            </div>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
      </table>
    </div>

    <!-- Mobile card list -->
    <div class="card-list mobile-only">
      @for (apt of appointments(); track apt.id) {
        <mat-card class="appointment-card">
          <mat-card-header>
            <mat-card-title>{{ apt.appointmentType }}</mat-card-title>
            <mat-card-subtitle>
              {{ apt.appointmentTime | date:'EEE, MMM d h:mm a' }}
            </mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <div class="card-detail">
              <span>Provider:</span>
              <span>{{ apt.providerName ?? 'TBD' }}</span>
            </div>
            <div class="card-detail">
              <span>Duration:</span>
              <span>{{ apt.durationMinutes }} min</span>
            </div>
            <div class="card-detail">
              <span>Status:</span>
              <mat-chip [class]="'status-' + apt.status.toLowerCase()"
                        class="card-chip">
                {{ apt.status }}
              </mat-chip>
            </div>
          </mat-card-content>
          <mat-card-actions>
            <button mat-stroked-button
                    [disabled]="!canModify(apt) || actionInProgress() !== null"
                    [matTooltip]="getActionTooltip(apt)"
                    (click)="rescheduleAppointment(apt)"
                    aria-label="Reschedule appointment">
              <mat-icon>edit_calendar</mat-icon> Reschedule
            </button>
            <button mat-stroked-button color="warn"
                    [disabled]="!canModify(apt) || actionInProgress() !== null"
                    [matTooltip]="getActionTooltip(apt)"
                    (click)="cancelAppointment(apt)"
                    aria-label="Cancel appointment">
              @if (actionInProgress() === apt.id) {
                <mat-spinner diameter="18"></mat-spinner>
              } @else {
                <mat-icon>cancel</mat-icon>
              }
              Cancel
            </button>
          </mat-card-actions>
        </mat-card>
      }
    </div>

    <!-- Pagination -->
    <mat-paginator [length]="totalCount()"
                   [pageSize]="pageSize()"
                   [pageIndex]="page()"
                   [pageSizeOptions]="[5, 10, 25]"
                   (page)="onPageChange($event)"
                   aria-label="Appointment pagination">
    </mat-paginator>
  }
</div>
```

6. **Create responsive styles**:

```scss
// client/src/app/features/appointments/appointment-history.component.scss

.history-container {
  padding: 24px 16px;
  max-width: 1200px;
  margin: 0 auto;

  h1 {
    margin-bottom: 24px;
  }
}

// Filter bar (SCR-007 Default)
.filter-bar {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 24px;

  mat-form-field {
    flex: 1;
    min-width: 160px;
  }
}

// Skeleton loading (SCR-007 Loading)
.skeleton-table {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.skeleton-row {
  height: 48px;
  background: linear-gradient(
    90deg,
    var(--mat-sys-surface-variant) 25%,
    var(--mat-sys-surface) 50%,
    var(--mat-sys-surface-variant) 75%
  );
  background-size: 200% 100%;
  animation: shimmer 1.5s infinite;
  border-radius: 4px;
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

// Error banner (SCR-007 Error)
.error-banner {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  background: var(--mat-sys-error-container);
  border-radius: 8px;
  margin-bottom: 24px;
}

// Empty state (SCR-007 Empty)
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 64px 16px;
  text-align: center;

  .empty-icon {
    font-size: 64px;
    height: 64px;
    width: 64px;
    color: var(--mat-sys-on-surface-variant);
    margin-bottom: 16px;
  }
}

// Table
.table-wrapper {
  overflow-x: auto;
}

.action-buttons {
  display: flex;
  gap: 4px;
}

// Status chips
.status-confirmed {
  background: var(--mat-sys-tertiary-container) !important;
  color: var(--mat-sys-on-tertiary-container) !important;
}

.status-cancelled {
  background: var(--mat-sys-error-container) !important;
  color: var(--mat-sys-on-error-container) !important;
}

.status-completed {
  background: var(--mat-sys-surface-variant) !important;
  color: var(--mat-sys-on-surface-variant) !important;
}

.status-noshow {
  background: var(--mat-sys-outline-variant) !important;
}

// Mobile cards
.card-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.appointment-card {
  mat-card-actions {
    display: flex;
    gap: 8px;
    padding: 8px 16px;
  }
}

.card-detail {
  display: flex;
  justify-content: space-between;
  padding: 4px 0;

  span:first-child {
    font-weight: 500;
    color: var(--mat-sys-on-surface-variant);
  }
}

.card-chip {
  transform: scale(0.85);
}

// UXR-301: Responsive breakpoints
.desktop-only { display: block; }
.mobile-only { display: none; }

// Mobile (375px)
@media (max-width: 599px) {
  .desktop-only { display: none; }
  .mobile-only { display: block; }

  .history-container {
    padding: 16px 12px;
  }

  .filter-bar {
    flex-direction: column;

    mat-form-field {
      width: 100%;
    }
  }
}

// Tablet (768px)
@media (min-width: 600px) and (max-width: 1023px) {
  .history-container {
    padding: 24px;
  }
}

// Desktop (1440px)
@media (min-width: 1024px) {
  .history-container {
    padding: 32px;
  }
}
```

7. **Add routes** to application routing:

```typescript
// Add to client/src/app/app.routes.ts
{
  path: 'appointments',
  loadComponent: () =>
    import('./features/appointments/appointment-history.component')
      .then(m => m.AppointmentHistoryComponent),
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
            │   ├── scheduling/                     (existing from US_019)
            │   ├── intake/                         (existing from US_020)
            │   ├── booking/                        (existing from US_021)
            │   └── appointments/                   (new module)
            │       ├── appointment-history.component.ts
            │       ├── appointment-history.component.html
            │       ├── appointment-history.component.scss
            │       ├── cancel-dialog.component.ts
            │       ├── appointment-api.service.ts
            │       └── models/
            │           └── appointment.models.ts
            └── app.routes.ts                       (modify — add appointments route)
```

> Placeholder: Update on execution based on US_022 task_001 and US_021 task_003 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/appointments/models/appointment.models.ts | TypeScript interfaces for appointment list, cancel/reschedule request/response, filters |
| CREATE | client/src/app/features/appointments/appointment-api.service.ts | HttpClient service for getAppointments, cancelAppointment, rescheduleAppointment |
| CREATE | client/src/app/features/appointments/cancel-dialog.component.ts | UXR-111 destructive action confirmation dialog with optional override reason |
| CREATE | client/src/app/features/appointments/appointment-history.component.ts | Standalone component with filter state, table/card views, cancel/reschedule actions |
| CREATE | client/src/app/features/appointments/appointment-history.component.html | Template with filter bar, desktop table, mobile cards, all 5 SCR-007 states |
| CREATE | client/src/app/features/appointments/appointment-history.component.scss | Responsive styles with 375px, 768px, 1440px breakpoints, skeleton loading |
| MODIFY | client/src/app/app.routes.ts | Add lazy-loaded appointment history route with auth guard |

## External References

- Angular Material Table: https://material.angular.io/components/table/overview
- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- Angular Material Paginator: https://material.angular.io/components/paginator/overview
- Angular Signals: https://angular.dev/guide/signals

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Navigate to: http://localhost:4200/appointments
```

## Implementation Validation Strategy

- [ ] Appointment history table displays with date, type, provider, duration, status columns (SCR-007 Default)
- [ ] Filter bar with status dropdown, date range filters works and reloads data (SCR-007 Default)
- [ ] Skeleton loading rows render during data fetch (SCR-007 Loading)
- [ ] Empty state with illustration and "Book an Appointment" CTA appears when no results (SCR-007 Empty)
- [ ] Error retry banner appears on load failure (SCR-007 Error)
- [ ] Cancel button opens confirmation dialog per UXR-111 (destructive action)
- [ ] Patient cannot cancel/reschedule within 24 hours — buttons disabled with tooltip (AC-3)
- [ ] Staff can cancel/reschedule within 24 hours with mandatory reason textarea in dialog (AC-4)
- [ ] Successful cancel updates row status to "Cancelled" with success toast (AC-1)
- [ ] Reschedule navigates to slot search with appointment context (AC-2)
- [ ] Loading spinner on cancel button during API call (UXR-501)
- [ ] Mobile card list view at 375px, full table at 1024px+ (UXR-301)
- [ ] `aria-label` attributes on all interactive elements (NFR-009 WCAG 2.1 AA)

## Implementation Checklist

- [ ] Create appointment list, cancel/reschedule TypeScript interfaces
- [ ] Create `AppointmentApiService` with list, cancel, and reschedule methods
- [ ] Create `CancelDialogComponent` with UXR-111 confirmation pattern and optional override reason
- [ ] Create `AppointmentHistoryComponent` with all 5 SCR-007 states (Default, Loading, Empty, Error, Validation)
- [ ] Create template with desktop table and mobile card list views
- [ ] Create SCSS with responsive breakpoints (375px, 768px, 1440px) and skeleton animation
- [ ] Add lazy-loaded appointment history route to `app.routes.ts`
- [ ] Verify `aria-label` on all buttons, tooltips, and form fields
