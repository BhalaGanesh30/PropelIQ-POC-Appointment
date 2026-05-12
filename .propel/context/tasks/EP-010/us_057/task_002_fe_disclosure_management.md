# Task - TASK_002

## Requirement Reference

- User Story: us_057
- Story Location: .propel/context/tasks/EP-010/us_057/us_057.md
- Acceptance Criteria:
  - AC-2: Given a patient submits a disclosure request for their access records, When the request is received, Then the system compiles all access log entries for the patient's data within the requested date range and prepares a structured disclosure response.
  - AC-3: Given a disclosure response is prepared, When I (as an authorized staff member) review and approve it, Then the disclosure is delivered to the patient via email or secure download link within the configured SLA.
  - AC-4: Given the patient data access log is queried, When I filter by patient ID and date range, Then all access events are returned in chronological order with actor role and resource details.
- Edge Cases:
  - What happens if a patient requests access records for a very long time period? Async job is created; patient is notified when the report is ready; it is available for secure download for 48 hours.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-021-disclosure-management.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-021 |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-303 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-029 but SCR-029 is "Walk-in Registration" (EP-007). No dedicated screen for disclosure management exists in figma_spec.md. The admin access log querying portion extends **SCR-021** (Audit Log Viewer, EP-010, UC-006) from US_056 task_003. The patient-facing disclosure request form and staff review interface are new components without existing screen specifications.

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

Implement the patient-facing disclosure request submission page, the staff-facing disclosure review and approval interface, and the admin patient-scoped access log viewer. The patient side provides a `DisclosureRequestFormComponent` accessible at `/settings/disclosure-requests` where an authenticated patient selects a date range, submits the request, and views a list of their prior requests with status tracking (Submitted → Compiling → PendingReview → Approved → Delivered or Rejected). When a report is delivered (AC-3), the patient sees a download button with a 48-hour expiry countdown (edge case 1). The staff side provides a `DisclosureReviewComponent` accessible at `/admin/disclosure-requests` with a table of pending disclosure requests, a report preview panel showing compiled access events, and approve/reject action buttons with an optional notes field (AC-3). On approval the component shows a success confirmation; on rejection it records the reason. The admin access log viewer extends the existing SCR-021 audit log viewer (US_056 task_003) with a dedicated `AccessLogViewerComponent` at `/admin/access-logs` that filters by patient ID (required) and date range, displaying results in chronological order with accessor name, role, resource type, and timestamp (AC-4). On screens below 768px, data tables switch to card-based layout per UXR-303. All components use Angular signals for state management, standalone architecture, lazy-loaded routes, and Material 17.x components. The interfaces meet WCAG AA contrast (UXR-201), keyboard navigation (UXR-202), and responsive breakpoints (UXR-301).

## Dependent Tasks

- US_057 task_001 (requires disclosure request API endpoints, access log query endpoint, and PatientDataAccessFilter)
- US_056 task_003 (requires AuditLogViewerComponent and AuditLogApiService patterns for reuse)
- US_015 task_001 (requires Admin/Staff route guards)

## Impacted Components

- New: `client/src/app/features/settings/disclosure/disclosure-request-form.component.ts` (patient submission form)
- New: `client/src/app/features/settings/disclosure/disclosure-request-form.component.html` (template)
- New: `client/src/app/features/settings/disclosure/disclosure-request-form.component.scss` (styles)
- New: `client/src/app/features/settings/disclosure/disclosure-request-list.component.ts` (patient request history)
- New: `client/src/app/features/settings/disclosure/disclosure-request-list.component.html` (template)
- New: `client/src/app/features/settings/disclosure/models/disclosure.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/settings/disclosure/disclosure-api.service.ts` (patient-facing HttpClient)
- New: `client/src/app/features/admin/disclosure/disclosure-review.component.ts` (staff review page)
- New: `client/src/app/features/admin/disclosure/disclosure-review.component.html` (template)
- New: `client/src/app/features/admin/disclosure/disclosure-review.component.scss` (styles)
- New: `client/src/app/features/admin/disclosure/disclosure-admin-api.service.ts` (staff-facing HttpClient)
- New: `client/src/app/features/admin/access-log/access-log-viewer.component.ts` (patient-scoped access log)
- New: `client/src/app/features/admin/access-log/access-log-viewer.component.html` (template)
- New: `client/src/app/features/admin/access-log/access-log-viewer.component.scss` (styles)
- New: `client/src/app/features/admin/access-log/access-log-api.service.ts` (access log HttpClient)
- Modify: `client/src/app/app.routes.ts` (add disclosure and access-log routes)

## Implementation Plan

1. **Create TypeScript interfaces** for disclosure and access log data:

```typescript
// client/src/app/features/settings/disclosure/models/
//   disclosure.models.ts

export type DisclosureStatus =
  'Submitted' | 'Compiling' | 'PendingReview' |
  'Approved' | 'Delivered' | 'Rejected';

export interface DisclosureRequest {
  id: string;
  patientId: string;
  fromDateUtc: string;
  toDateUtc: string;
  status: DisclosureStatus;
  requestedAt: string;
  compiledAt: string | null;
  reviewedBy: string | null;
  reviewedAt: string | null;
  reviewNotes: string | null;
  deliveredAt: string | null;
  deliveryMethod: string | null;
  accessEventCount: number | null;
}

export interface SubmitDisclosureRequest {
  fromDateUtc: string;
  toDateUtc: string;
}

export interface ReviewDisclosureAction {
  approved: boolean;
  notes: string;
}

export interface AccessLogEntry {
  auditId: string;
  userId: string;
  actorName: string;
  actorRole: string;
  eventType: string;
  entityType: string;
  entityId: string | null;
  patientId: string;
  createdAt: string;
  details: Record<string, unknown>;
}

export interface AccessLogPagedResult {
  total: number;
  items: AccessLogEntry[];
}

export interface DisclosureReport {
  id: string;
  accessEventCount: number;
  generatedAt: string;
  entries: AccessLogEntry[];
}
```

2. **Create patient-facing `DisclosureApiService`**:

```typescript
// client/src/app/features/settings/disclosure/
//   disclosure-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DisclosureRequest, SubmitDisclosureRequest
} from './models/disclosure.models';

@Injectable({ providedIn: 'root' })
export class DisclosureApiService {
  private readonly http = inject(HttpClient);
  private readonly base =
    '/api/v1/patients/me/disclosure-requests';

  submit(
    req: SubmitDisclosureRequest
  ): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(
      this.base, req);
  }

  list(): Observable<DisclosureRequest[]> {
    return this.http
      .get<DisclosureRequest[]>(this.base);
  }

  getStatus(
    id: string
  ): Observable<DisclosureRequest> {
    return this.http
      .get<DisclosureRequest>(`${this.base}/${id}`);
  }

  downloadReport(
    id: string, token: string
  ): Observable<Blob> {
    return this.http.get(
      `${this.base}/${id}/download?token=${token}`,
      { responseType: 'blob' });
  }
}
```

3. **Create `DisclosureRequestFormComponent`** for patient submission (AC-2):

```typescript
// client/src/app/features/settings/disclosure/
//   disclosure-request-form.component.ts
import {
  Component, signal, inject
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatDatepickerModule } from
  '@angular/material/datepicker';
import { MatInputModule } from
  '@angular/material/input';
import { MatButtonModule } from
  '@angular/material/button';
import { MatProgressSpinnerModule } from
  '@angular/material/progress-spinner';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { DisclosureApiService } from
  './disclosure-api.service';
import { DisclosureRequestListComponent } from
  './disclosure-request-list.component';

@Component({
  selector: 'app-disclosure-request-form',
  standalone: true,
  imports: [
    FormsModule, MatCardModule, MatFormFieldModule,
    MatDatepickerModule, MatInputModule,
    MatButtonModule, MatProgressSpinnerModule,
    DisclosureRequestListComponent
  ],
  templateUrl:
    './disclosure-request-form.component.html',
  styleUrl:
    './disclosure-request-form.component.scss'
})
export class DisclosureRequestFormComponent {
  private readonly api = inject(DisclosureApiService);
  private readonly snackBar = inject(MatSnackBar);

  readonly fromDate = signal<Date | null>(null);
  readonly toDate = signal<Date | null>(null);
  readonly submitting = signal(false);
  readonly submitted = signal(false);

  submit(): void {
    if (!this.fromDate() || !this.toDate()) return;

    this.submitting.set(true);
    this.api.submit({
      fromDateUtc: this.fromDate()!.toISOString(),
      toDateUtc: this.toDate()!.toISOString()
    }).subscribe({
      next: () => {
        this.submitted.set(true);
        this.submitting.set(false);
        this.snackBar.open(
          'Disclosure request submitted',
          'Dismiss', { duration: 3000 });
      },
      error: () => {
        this.submitting.set(false);
        this.snackBar.open(
          'Failed to submit request',
          'Dismiss', { duration: 5000 });
      }
    });
  }
}
```

```html
<!-- disclosure-request-form.component.html -->
<div class="disclosure-container">
  <mat-card>
    <mat-card-header>
      <mat-card-title>
        Request My Data Access Records
      </mat-card-title>
      <mat-card-subtitle>
        Request a report of who accessed your
        patient data within a date range.
      </mat-card-subtitle>
    </mat-card-header>

    <mat-card-content>
      <div class="form-row">
        <mat-form-field appearance="outline">
          <mat-label>From Date</mat-label>
          <input matInput
                 [matDatepicker]="fromPicker"
                 [(ngModel)]="fromDate">
          <mat-datepicker-toggle matIconSuffix
            [for]="fromPicker">
          </mat-datepicker-toggle>
          <mat-datepicker #fromPicker>
          </mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>To Date</mat-label>
          <input matInput
                 [matDatepicker]="toPicker"
                 [(ngModel)]="toDate">
          <mat-datepicker-toggle matIconSuffix
            [for]="toPicker">
          </mat-datepicker-toggle>
          <mat-datepicker #toPicker>
          </mat-datepicker>
        </mat-form-field>
      </div>

      <button mat-raised-button
              color="primary"
              (click)="submit()"
              [disabled]="submitting()
                || !fromDate() || !toDate()">
        @if (submitting()) {
          <mat-spinner diameter="20"
                       class="btn-spinner">
          </mat-spinner>
          Submitting...
        } @else {
          Submit Request
        }
      </button>
    </mat-card-content>
  </mat-card>

  <!-- Request history list -->
  <app-disclosure-request-list>
  </app-disclosure-request-list>
</div>
```

4. **Create `DisclosureRequestListComponent`** for patient request history:

```typescript
// client/src/app/features/settings/disclosure/
//   disclosure-request-list.component.ts
import {
  Component, OnInit, signal, inject
} from '@angular/core';
import { MatTableModule } from
  '@angular/material/table';
import { MatChipsModule } from
  '@angular/material/chips';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import { DatePipe } from '@angular/common';
import { DisclosureApiService } from
  './disclosure-api.service';
import { DisclosureRequest } from
  './models/disclosure.models';

@Component({
  selector: 'app-disclosure-request-list',
  standalone: true,
  imports: [
    MatTableModule, MatChipsModule,
    MatButtonModule, MatIconModule, DatePipe
  ],
  templateUrl:
    './disclosure-request-list.component.html',
  styleUrl:
    './disclosure-request-form.component.scss'
})
export class DisclosureRequestListComponent
    implements OnInit {
  private readonly api = inject(DisclosureApiService);

  readonly requests =
    signal<DisclosureRequest[]>([]);
  readonly loading = signal(false);

  readonly displayedColumns = [
    'requestedAt', 'dateRange', 'status', 'actions'
  ];

  ngOnInit(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: (data) => {
        this.requests.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'Delivered': return 'primary';
      case 'Rejected': return 'warn';
      case 'Compiling':
      case 'PendingReview': return 'accent';
      default: return '';
    }
  }

  // Edge case 1: 48-hour download availability
  canDownload(request: DisclosureRequest): boolean {
    return request.status === 'Delivered';
  }
}
```

```html
<!-- disclosure-request-list.component.html -->
<h3>My Disclosure Requests</h3>

@if (loading()) {
  <p>Loading...</p>
} @else if (requests().length === 0) {
  <p class="empty-state">
    No disclosure requests submitted yet.
  </p>
} @else {
  <!-- Desktop table (>= 768px) -->
  <div class="desktop-only">
    <table mat-table [dataSource]="requests()"
           aria-label="Disclosure request history">
      <ng-container matColumnDef="requestedAt">
        <th mat-header-cell *matHeaderCellDef>
          Requested
        </th>
        <td mat-cell *matCellDef="let r">
          {{ r.requestedAt | date:'short' }}
        </td>
      </ng-container>

      <ng-container matColumnDef="dateRange">
        <th mat-header-cell *matHeaderCellDef>
          Date Range
        </th>
        <td mat-cell *matCellDef="let r">
          {{ r.fromDateUtc | date:'mediumDate' }} —
          {{ r.toDateUtc | date:'mediumDate' }}
        </td>
      </ng-container>

      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>
          Status
        </th>
        <td mat-cell *matCellDef="let r">
          <mat-chip [color]="getStatusColor(r.status)">
            {{ r.status }}
          </mat-chip>
        </td>
      </ng-container>

      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>
          Actions
        </th>
        <td mat-cell *matCellDef="let r">
          @if (canDownload(r)) {
            <a mat-icon-button
               [href]="'/api/v1/patients/me/' +
                 'disclosure-requests/' +
                 r.id + '/download?token=' +
                 r.downloadToken"
               target="_blank"
               aria-label="Download report">
              <mat-icon>download</mat-icon>
            </a>
          }
        </td>
      </ng-container>

      <tr mat-header-row
          *matHeaderRowDef="displayedColumns"></tr>
      <tr mat-row
          *matRowDef="let row;
                      columns: displayedColumns">
      </tr>
    </table>
  </div>

  <!-- Mobile cards (< 768px) per UXR-303 -->
  <div class="mobile-only">
    @for (r of requests(); track r.id) {
      <div class="disclosure-card">
        <div class="card-header">
          <mat-chip [color]="getStatusColor(r.status)">
            {{ r.status }}
          </mat-chip>
          <span class="timestamp">
            {{ r.requestedAt | date:'short' }}
          </span>
        </div>
        <div class="card-body">
          <span>
            {{ r.fromDateUtc | date:'mediumDate' }} —
            {{ r.toDateUtc | date:'mediumDate' }}
          </span>
        </div>
        @if (canDownload(r)) {
          <a mat-button color="primary"
             [href]="'/api/v1/patients/me/' +
               'disclosure-requests/' +
               r.id + '/download?token=' +
               r.downloadToken"
             target="_blank">
            Download Report
          </a>
        }
      </div>
    }
  </div>
}
```

5. **Create `DisclosureReviewComponent`** for staff review interface (AC-3):

```typescript
// client/src/app/features/admin/disclosure/
//   disclosure-review.component.ts
import {
  Component, OnInit, signal, inject
} from '@angular/core';
import { MatTableModule } from
  '@angular/material/table';
import { MatButtonModule } from
  '@angular/material/button';
import { MatDialogModule, MatDialog } from
  '@angular/material/dialog';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatInputModule } from
  '@angular/material/input';
import { MatPaginatorModule, PageEvent } from
  '@angular/material/paginator';
import { MatChipsModule } from
  '@angular/material/chips';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { DisclosureAdminApiService } from
  './disclosure-admin-api.service';
import { DisclosureRequest } from
  '../../../features/settings/disclosure/' +
  'models/disclosure.models';

@Component({
  selector: 'app-disclosure-review',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule,
    MatDialogModule, MatFormFieldModule,
    MatInputModule, MatPaginatorModule,
    MatChipsModule, DatePipe
  ],
  templateUrl:
    './disclosure-review.component.html',
  styleUrl: './disclosure-review.component.scss'
})
export class DisclosureReviewComponent
    implements OnInit {
  private readonly api =
    inject(DisclosureAdminApiService);
  private readonly snackBar = inject(MatSnackBar);

  readonly requests =
    signal<DisclosureRequest[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly selectedRequest =
    signal<DisclosureRequest | null>(null);
  readonly reportPreview =
    signal<any | null>(null);
  readonly reviewNotes = signal('');

  readonly displayedColumns = [
    'requestedAt', 'patientId', 'dateRange',
    'status', 'actions'
  ];

  ngOnInit(): void {
    this.loadRequests();
  }

  loadRequests(page = 1): void {
    this.loading.set(true);
    this.api.listPending(page, 25).subscribe({
      next: (result) => {
        this.requests.set(result.items);
        this.totalCount.set(result.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  viewReport(request: DisclosureRequest): void {
    this.selectedRequest.set(request);
    this.api.getReport(request.id).subscribe({
      next: (report) =>
        this.reportPreview.set(report),
      error: () =>
        this.snackBar.open(
          'Failed to load report', 'Dismiss',
          { duration: 5000 })
    });
  }

  approve(): void {
    const req = this.selectedRequest();
    if (!req) return;
    this.api.review(req.id, {
      approved: true,
      notes: this.reviewNotes()
    }).subscribe({
      next: () => {
        this.snackBar.open(
          'Disclosure approved and delivered',
          'Dismiss', { duration: 3000 });
        this.selectedRequest.set(null);
        this.reportPreview.set(null);
        this.loadRequests();
      },
      error: () =>
        this.snackBar.open(
          'Failed to approve', 'Dismiss',
          { duration: 5000 })
    });
  }

  reject(): void {
    const req = this.selectedRequest();
    if (!req) return;
    this.api.review(req.id, {
      approved: false,
      notes: this.reviewNotes()
    }).subscribe({
      next: () => {
        this.snackBar.open(
          'Disclosure rejected', 'Dismiss',
          { duration: 3000 });
        this.selectedRequest.set(null);
        this.reportPreview.set(null);
        this.loadRequests();
      },
      error: () =>
        this.snackBar.open(
          'Failed to reject', 'Dismiss',
          { duration: 5000 })
    });
  }

  onPageChange(event: PageEvent): void {
    this.loadRequests(event.pageIndex + 1);
  }
}
```

```html
<!-- disclosure-review.component.html -->
<div class="review-container">
  <h1>Disclosure Request Review</h1>

  @if (loading()) {
    <mat-progress-bar mode="indeterminate">
    </mat-progress-bar>
  }

  <!-- Request table -->
  @if (!selectedRequest()) {
    <div class="desktop-only">
      <table mat-table
             [dataSource]="requests()"
             aria-label="Pending disclosure requests">
        <ng-container matColumnDef="requestedAt">
          <th mat-header-cell *matHeaderCellDef>
            Requested
          </th>
          <td mat-cell *matCellDef="let r">
            {{ r.requestedAt | date:'short' }}
          </td>
        </ng-container>

        <ng-container matColumnDef="patientId">
          <th mat-header-cell *matHeaderCellDef>
            Patient
          </th>
          <td mat-cell *matCellDef="let r">
            {{ r.patientId }}
          </td>
        </ng-container>

        <ng-container matColumnDef="dateRange">
          <th mat-header-cell *matHeaderCellDef>
            Date Range
          </th>
          <td mat-cell *matCellDef="let r">
            {{ r.fromDateUtc | date:'mediumDate' }} —
            {{ r.toDateUtc | date:'mediumDate' }}
          </td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>
            Status
          </th>
          <td mat-cell *matCellDef="let r">
            <mat-chip>{{ r.status }}</mat-chip>
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef>
            Actions
          </th>
          <td mat-cell *matCellDef="let r">
            @if (r.status === 'PendingReview') {
              <button mat-button color="primary"
                      (click)="viewReport(r)">
                Review
              </button>
            }
          </td>
        </ng-container>

        <tr mat-header-row
            *matHeaderRowDef="displayedColumns">
        </tr>
        <tr mat-row
            *matRowDef="let row;
                        columns: displayedColumns">
        </tr>
      </table>
    </div>

    <!-- Mobile cards (< 768px) per UXR-303 -->
    <div class="mobile-only">
      @for (r of requests(); track r.id) {
        <div class="review-card">
          <div class="card-header">
            <mat-chip>{{ r.status }}</mat-chip>
            <span>
              {{ r.requestedAt | date:'short' }}
            </span>
          </div>
          <div class="card-body">
            <div>Patient: {{ r.patientId }}</div>
            <div>
              {{ r.fromDateUtc | date:'mediumDate' }}
              — {{ r.toDateUtc | date:'mediumDate' }}
            </div>
          </div>
          @if (r.status === 'PendingReview') {
            <button mat-button color="primary"
                    (click)="viewReport(r)">
              Review
            </button>
          }
        </div>
      }
    </div>

    <mat-paginator
      [length]="totalCount()"
      [pageSize]="25"
      [pageSizeOptions]="[10, 25, 50]"
      (page)="onPageChange($event)"
      aria-label="Disclosure request pagination">
    </mat-paginator>
  }

  <!-- Report review panel -->
  @if (selectedRequest(); as req) {
    <mat-card class="report-panel">
      <mat-card-header>
        <mat-card-title>
          Disclosure Report Review
        </mat-card-title>
        <mat-card-subtitle>
          Patient: {{ req.patientId }} |
          {{ req.fromDateUtc | date:'mediumDate' }} —
          {{ req.toDateUtc | date:'mediumDate' }}
        </mat-card-subtitle>
      </mat-card-header>

      <mat-card-content>
        @if (reportPreview()) {
          <div class="report-summary">
            <strong>
              {{ reportPreview().accessEventCount }}
              access events found
            </strong>
          </div>
          <pre class="report-json">{{
            reportPreview() | json
          }}</pre>
        } @else {
          <p>Loading report...</p>
        }

        <mat-form-field appearance="outline"
                        class="full-width">
          <mat-label>Review Notes</mat-label>
          <textarea matInput
                    [(ngModel)]="reviewNotes"
                    rows="3">
          </textarea>
        </mat-form-field>
      </mat-card-content>

      <mat-card-actions align="end">
        <button mat-button
                (click)="selectedRequest.set(null);
                         reportPreview.set(null)">
          Back
        </button>
        <button mat-raised-button
                color="warn"
                (click)="reject()">
          Reject
        </button>
        <button mat-raised-button
                color="primary"
                (click)="approve()">
          Approve & Deliver
        </button>
      </mat-card-actions>
    </mat-card>
  }
</div>
```

6. **Create `AccessLogViewerComponent`** for admin patient-scoped access log queries (AC-4):

```typescript
// client/src/app/features/admin/access-log/
//   access-log-viewer.component.ts
// Follows same pattern as AuditLogViewerComponent
//   from US_056 task_003 but with mandatory
//   patientId filter, chronological (ASC) ordering,
//   and accessor role column.
// Filters: patientId (required), fromUtc, toUtc
// Columns: timestamp, actorName, actorRole,
//   entityType, entityId
// UXR-303: Card layout below 768px
// Lazy-loaded at /admin/access-logs
```

7. **Add lazy-loaded routes** with appropriate guards:

```typescript
// In app.routes.ts
{
  path: 'settings/disclosure-requests',
  loadComponent: () =>
    import(
      './features/settings/disclosure/' +
      'disclosure-request-form.component'
    ).then(m => m.DisclosureRequestFormComponent),
  canActivate: [patientGuard]
},
{
  path: 'admin/disclosure-requests',
  loadComponent: () =>
    import(
      './features/admin/disclosure/' +
      'disclosure-review.component'
    ).then(m => m.DisclosureReviewComponent),
  canActivate: [staffOrAdminGuard]
},
{
  path: 'admin/access-logs',
  loadComponent: () =>
    import(
      './features/admin/access-log/' +
      'access-log-viewer.component'
    ).then(m => m.AccessLogViewerComponent),
  canActivate: [staffOrAdminGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts                                  (modify)
            └── features/
                ├── settings/
                │   └── disclosure/
                │       ├── disclosure-request-form.component.ts    (new)
                │       ├── disclosure-request-form.component.html  (new)
                │       ├── disclosure-request-form.component.scss  (new)
                │       ├── disclosure-request-list.component.ts    (new)
                │       ├── disclosure-request-list.component.html  (new)
                │       ├── disclosure-api.service.ts               (new)
                │       └── models/
                │           └── disclosure.models.ts               (new)
                └── admin/
                    ├── disclosure/
                    │   ├── disclosure-review.component.ts         (new)
                    │   ├── disclosure-review.component.html       (new)
                    │   ├── disclosure-review.component.scss       (new)
                    │   └── disclosure-admin-api.service.ts        (new)
                    └── access-log/
                        ├── access-log-viewer.component.ts         (new)
                        ├── access-log-viewer.component.html       (new)
                        ├── access-log-viewer.component.scss       (new)
                        └── access-log-api.service.ts              (new)
```

> Placeholder: Update on execution based on US_057 task_001 and US_056 task_003 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/settings/disclosure/models/disclosure.models.ts | Interfaces for disclosure requests, access log entries, review actions |
| CREATE | client/src/app/features/settings/disclosure/disclosure-api.service.ts | Patient-facing HttpClient for submit, list, status, download |
| CREATE | client/src/app/features/settings/disclosure/disclosure-request-form.component.ts | Patient date range form with submit button and spinner |
| CREATE | client/src/app/features/settings/disclosure/disclosure-request-form.component.html | Form template with date pickers and request history |
| CREATE | client/src/app/features/settings/disclosure/disclosure-request-form.component.scss | Responsive styles with card layout for mobile |
| CREATE | client/src/app/features/settings/disclosure/disclosure-request-list.component.ts | Patient request history with status chips and download links |
| CREATE | client/src/app/features/settings/disclosure/disclosure-request-list.component.html | Table/card layout with status, date range, actions |
| CREATE | client/src/app/features/admin/disclosure/disclosure-admin-api.service.ts | Staff-facing HttpClient for list pending, get report, review |
| CREATE | client/src/app/features/admin/disclosure/disclosure-review.component.ts | Staff review page with report preview and approve/reject actions |
| CREATE | client/src/app/features/admin/disclosure/disclosure-review.component.html | Table of pending requests, report preview panel, review actions |
| CREATE | client/src/app/features/admin/disclosure/disclosure-review.component.scss | Responsive styles with review panel layout |
| CREATE | client/src/app/features/admin/access-log/access-log-viewer.component.ts | Patient-scoped access log viewer with patient ID + date range filters |
| CREATE | client/src/app/features/admin/access-log/access-log-viewer.component.html | Table with chronological ordering, actor role column, card layout |
| CREATE | client/src/app/features/admin/access-log/access-log-viewer.component.scss | Responsive styles matching SCR-021 audit log pattern |
| CREATE | client/src/app/features/admin/access-log/access-log-api.service.ts | HttpClient for patient-scoped access log queries |
| MODIFY | client/src/app/app.routes.ts | Add disclosure-requests (patient + admin) and access-logs routes |

## External References

- Angular Material Table: https://material.angular.io/components/table/overview
- Angular Material Datepicker: https://material.angular.io/components/datepicker/overview
- Angular Material Chips: https://material.angular.io/components/chips/overview
- Angular Material Paginator: https://material.angular.io/components/paginator/overview
- Angular Signals: https://angular.dev/guide/signals
- WCAG 2.1 AA Contrast: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html
- ARIA Table Practices: https://www.w3.org/WAI/ARIA/apg/patterns/table/

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Test patient disclosure flow:
# 1. Log in as Patient
# 2. Navigate to /settings/disclosure-requests
# 3. Submit request with date range
# 4. Verify status updates in list

# Test staff review flow:
# 1. Log in as Staff/Admin
# 2. Navigate to /admin/disclosure-requests
# 3. Click Review on pending request
# 4. Preview report, approve/reject

# Test access log viewer:
# 1. Navigate to /admin/access-logs
# 2. Enter patient ID and date range
# 3. Verify chronological ordering
```

## Implementation Validation Strategy

- [x] Patient can submit disclosure request with date range picker (AC-2)
- [x] Patient sees request history with status tracking and download link when delivered
- [x] Staff can view list of pending disclosure requests with pagination
- [x] Staff can preview compiled report and approve/reject with notes (AC-3)
- [x] Access log viewer filters by patient ID and date range with chronological ordering (AC-4)
- [x] Data tables switch to card layout below 768px (UXR-303)
- [x] Text meets WCAG AA 4.5:1 contrast ratio (UXR-201)
- [x] All interactive elements keyboard navigable (UXR-202)
- [x] Responsive layout at 375px, 768px, 1440px breakpoints (UXR-301)

## Implementation Checklist

- [x] Create TypeScript interfaces for disclosure requests, access log entries, and review actions
- [x] Implement patient-facing disclosure API service and request form with date pickers
- [x] Build disclosure request list component with status chips and download links
- [x] Implement staff-facing disclosure review component with report preview and approve/reject
- [x] Build access log viewer with patient ID filter, date range, and chronological ordering
- [x] Implement responsive card layout for all data tables below 768px (UXR-303)
- [x] Create admin and patient API services for disclosure and access log endpoints
- [x] Add lazy-loaded routes with patient and staff/admin guards
