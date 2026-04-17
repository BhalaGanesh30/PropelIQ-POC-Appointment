# Task - TASK_003

## Requirement Reference

- User Story: us_056
- Story Location: .propel/context/tasks/EP-010/us_056/us_056.md
- Acceptance Criteria:
  - AC-4: Given an admin accesses the audit log viewer, When they query with filters (actor, action type, date range, resource ID), Then matching records are returned with pagination within 3 seconds.
- Edge Cases:
  - How does the system handle export of large audit log batches for external compliance review? Async export is triggered; when complete, the file is delivered via secure download link to the requesting admin.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-021-audit-log.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-021 |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-303 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-029 but SCR-029 is "Walk-in Registration" (EP-007). The actual Audit Log Viewer screen is **SCR-021** per figma_spec.md, which describes "Read-only audit trail viewer with filters for event type, user, date range, and entity. Restricted to admin access."

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

Implement the admin-only Audit Log Viewer page mapped to SCR-021 (EP-010, UC-006). The page provides a read-only data table with a filter bar above and pagination below, restricted to Admin role users. The filter bar includes controls for event type (select dropdown), actor (user search/autocomplete), date range (date range picker with UTC handling), and resource ID (text input). Applied filters render as removable chips above the data table (SCR-021 Validation state). The data table displays columns for timestamp, actor name, event type, entity type, entity ID, and a summary of the detail payload. Row click expands the event detail inline showing the full structured `details` JSONB content in a formatted JSON viewer. Pagination is numbered with Previous/Next controls (UXR-303). On screens below 768px, the data table switches to a card-based layout (UXR-303). The page includes an "Export" button that triggers the async export endpoint (`POST /api/v1/admin/audit-logs/export`) from task_001 and displays a progress indicator until the CSV download link is available (edge case 2). All five SCR-021 states are implemented: Default (filter bar + data table with pagination), Loading (skeleton rows + progress indicator), Empty ("No audit events match filters" + clear-filters CTA), Error (retry banner on load failure), and Validation (applied filters as removable chips). The component uses Angular signals for reactive state management, lazy-loaded via `/admin/audit-logs` route, and meets WCAG AA contrast (UXR-201), keyboard navigation (UXR-202), and responsive layout (UXR-301) requirements.

## Dependent Tasks

- US_056 task_001 (requires `GET /api/v1/admin/audit-logs` query API and `POST /api/v1/admin/audit-logs/export` endpoint)
- US_015 task_001 (requires Admin role RBAC policies for route guard)

## Impacted Components

- New: `client/src/app/features/admin/audit-log/audit-log-viewer.component.ts` (main page component)
- New: `client/src/app/features/admin/audit-log/audit-log-viewer.component.html` (template)
- New: `client/src/app/features/admin/audit-log/audit-log-viewer.component.scss` (styles)
- New: `client/src/app/features/admin/audit-log/audit-log-filter.component.ts` (filter bar component)
- New: `client/src/app/features/admin/audit-log/audit-log-filter.component.html` (filter template)
- New: `client/src/app/features/admin/audit-log/audit-log-detail.component.ts` (inline detail expansion)
- New: `client/src/app/features/admin/audit-log/models/audit-log.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/admin/audit-log/audit-log-api.service.ts` (HttpClient service)
- Modify: `client/src/app/app.routes.ts` (add /admin/audit-logs route with admin guard)

## Implementation Plan

1. **Create TypeScript interfaces** for audit log data and filter parameters:

```typescript
// client/src/app/features/admin/audit-log/models/
//   audit-log.models.ts

export interface AuditLogEntry {
  auditId: string;
  userId: string;
  actorName: string;
  eventType: string;
  entityType: string;
  entityId: string | null;
  details: Record<string, unknown>;
  createdAt: string;  // ISO 8601 UTC
}

export interface AuditLogQueryParams {
  actorId?: string;
  actionType?: string;
  fromUtc?: string;
  toUtc?: string;
  resourceId?: string;
  page: number;
  pageSize: number;
}

export interface AuditLogPagedResult {
  total: number;
  items: AuditLogEntry[];
}

export interface ExportJob {
  jobId: string;
}

export interface ExportStatus {
  status: 'Processing' | 'Complete';
  downloadUrl?: string;
}

export const EVENT_TYPES: string[] = [
  'Authentication',
  'DataAccess',
  'Override',
  'ConfigurationChange',
  'CodingReview',
  'BookingCreated',
  'BookingCancelled',
  'BookingRescheduled',
  'SessionCreated',
  'SessionExpired',
  'PasswordReset',
  'AccountLockout',
  'RoleAssignment'
];
```

2. **Create `AuditLogApiService`** for HTTP communication:

```typescript
// client/src/app/features/admin/audit-log/
//   audit-log-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from
  '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AuditLogQueryParams, AuditLogPagedResult,
  ExportJob, ExportStatus
} from './models/audit-log.models';

@Injectable({ providedIn: 'root' })
export class AuditLogApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl =
    '/api/v1/admin/audit-logs';

  query(
    params: AuditLogQueryParams
  ): Observable<AuditLogPagedResult> {
    let httpParams = new HttpParams()
      .set('page', params.page)
      .set('pageSize', params.pageSize);

    if (params.actorId)
      httpParams = httpParams.set(
        'actorId', params.actorId);
    if (params.actionType)
      httpParams = httpParams.set(
        'actionType', params.actionType);
    if (params.fromUtc)
      httpParams = httpParams.set(
        'fromUtc', params.fromUtc);
    if (params.toUtc)
      httpParams = httpParams.set(
        'toUtc', params.toUtc);
    if (params.resourceId)
      httpParams = httpParams.set(
        'resourceId', params.resourceId);

    return this.http
      .get<AuditLogPagedResult>(
        this.baseUrl, { params: httpParams });
  }

  triggerExport(
    params: AuditLogQueryParams
  ): Observable<ExportJob> {
    return this.http
      .post<ExportJob>(
        `${this.baseUrl}/export`, params);
  }

  getExportStatus(
    jobId: string
  ): Observable<ExportStatus> {
    return this.http
      .get<ExportStatus>(
        `${this.baseUrl}/export/${jobId}`);
  }
}
```

3. **Create `AuditLogFilterComponent`** with filter bar controls:

```typescript
// client/src/app/features/admin/audit-log/
//   audit-log-filter.component.ts
import {
  Component, output, signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatSelectModule } from
  '@angular/material/select';
import { MatInputModule } from
  '@angular/material/input';
import { MatDatepickerModule } from
  '@angular/material/datepicker';
import { MatChipsModule } from
  '@angular/material/chips';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import {
  AuditLogQueryParams, EVENT_TYPES
} from './models/audit-log.models';

@Component({
  selector: 'app-audit-log-filter',
  standalone: true,
  imports: [
    FormsModule, MatFormFieldModule,
    MatSelectModule, MatInputModule,
    MatDatepickerModule, MatChipsModule,
    MatButtonModule, MatIconModule
  ],
  templateUrl: './audit-log-filter.component.html'
})
export class AuditLogFilterComponent {
  readonly filtersChanged =
    output<AuditLogQueryParams>();
  readonly eventTypes = EVENT_TYPES;

  readonly selectedEventType = signal('');
  readonly actorSearch = signal('');
  readonly fromDate = signal<Date | null>(null);
  readonly toDate = signal<Date | null>(null);
  readonly resourceId = signal('');

  // SCR-021 Validation state: active filters
  //   shown as removable chips
  readonly activeFilters = signal<
    { key: string; label: string }[]>([]);

  applyFilters(): void {
    const filters: { key: string; label: string }[]
      = [];
    const params: AuditLogQueryParams = {
      page: 1, pageSize: 25
    };

    if (this.selectedEventType()) {
      params.actionType = this.selectedEventType();
      filters.push({
        key: 'actionType',
        label: `Type: ${this.selectedEventType()}`
      });
    }
    if (this.actorSearch()) {
      params.actorId = this.actorSearch();
      filters.push({
        key: 'actorId',
        label: `Actor: ${this.actorSearch()}`
      });
    }
    if (this.fromDate()) {
      params.fromUtc =
        this.fromDate()!.toISOString();
      filters.push({
        key: 'fromUtc',
        label: `From: ${this.fromDate()!
          .toLocaleDateString()}`
      });
    }
    if (this.toDate()) {
      params.toUtc =
        this.toDate()!.toISOString();
      filters.push({
        key: 'toUtc',
        label: `To: ${this.toDate()!
          .toLocaleDateString()}`
      });
    }
    if (this.resourceId()) {
      params.resourceId = this.resourceId();
      filters.push({
        key: 'resourceId',
        label: `Resource: ${this.resourceId()}`
      });
    }

    this.activeFilters.set(filters);
    this.filtersChanged.emit(params);
  }

  removeFilter(key: string): void {
    switch (key) {
      case 'actionType':
        this.selectedEventType.set(''); break;
      case 'actorId':
        this.actorSearch.set(''); break;
      case 'fromUtc':
        this.fromDate.set(null); break;
      case 'toUtc':
        this.toDate.set(null); break;
      case 'resourceId':
        this.resourceId.set(''); break;
    }
    this.applyFilters();
  }

  clearAll(): void {
    this.selectedEventType.set('');
    this.actorSearch.set('');
    this.fromDate.set(null);
    this.toDate.set(null);
    this.resourceId.set('');
    this.activeFilters.set([]);
    this.filtersChanged.emit(
      { page: 1, pageSize: 25 });
  }
}
```

```html
<!-- audit-log-filter.component.html -->
<div class="filter-bar" role="search"
     aria-label="Audit log filters">
  <mat-form-field appearance="outline">
    <mat-label>Event Type</mat-label>
    <mat-select
      [(value)]="selectedEventType"
      (selectionChange)="applyFilters()">
      <mat-option value="">All</mat-option>
      @for (type of eventTypes; track type) {
        <mat-option [value]="type">
          {{ type }}
        </mat-option>
      }
    </mat-select>
  </mat-form-field>

  <mat-form-field appearance="outline">
    <mat-label>Actor</mat-label>
    <input matInput
           [(ngModel)]="actorSearch"
           placeholder="User ID or name"
           (keyup.enter)="applyFilters()">
  </mat-form-field>

  <mat-form-field appearance="outline">
    <mat-label>From Date</mat-label>
    <input matInput
           [matDatepicker]="fromPicker"
           [(ngModel)]="fromDate">
    <mat-datepicker-toggle matIconSuffix
      [for]="fromPicker">
    </mat-datepicker-toggle>
    <mat-datepicker #fromPicker></mat-datepicker>
  </mat-form-field>

  <mat-form-field appearance="outline">
    <mat-label>To Date</mat-label>
    <input matInput
           [matDatepicker]="toPicker"
           [(ngModel)]="toDate">
    <mat-datepicker-toggle matIconSuffix
      [for]="toPicker">
    </mat-datepicker-toggle>
    <mat-datepicker #toPicker></mat-datepicker>
  </mat-form-field>

  <mat-form-field appearance="outline">
    <mat-label>Resource ID</mat-label>
    <input matInput
           [(ngModel)]="resourceId"
           placeholder="Entity UUID"
           (keyup.enter)="applyFilters()">
  </mat-form-field>

  <button mat-raised-button
          color="primary"
          (click)="applyFilters()">
    Search
  </button>
</div>

<!-- SCR-021 Validation: Active filter chips -->
@if (activeFilters().length > 0) {
  <mat-chip-set aria-label="Active filters">
    @for (f of activeFilters(); track f.key) {
      <mat-chip (removed)="removeFilter(f.key)"
                removable>
        {{ f.label }}
        <mat-icon matChipRemove>cancel</mat-icon>
      </mat-chip>
    }
    <button mat-button
            (click)="clearAll()"
            class="clear-all-btn">
      Clear All
    </button>
  </mat-chip-set>
}
```

4. **Create `AuditLogDetailComponent`** for inline row expansion:

```typescript
// client/src/app/features/admin/audit-log/
//   audit-log-detail.component.ts
import { Component, input } from '@angular/core';
import { JsonPipe } from '@angular/common';

@Component({
  selector: 'app-audit-log-detail',
  standalone: true,
  imports: [JsonPipe],
  template: `
    <div class="detail-panel"
         role="region"
         aria-label="Audit event details">
      <pre class="json-viewer">{{
        details() | json
      }}</pre>
    </div>
  `,
  styles: [`
    .detail-panel {
      padding: 16px 24px;
      background: #FAFAFA;
      border-top: 1px solid #E0E0E0;
    }
    .json-viewer {
      font-family: 'Roboto Mono', monospace;
      font-size: 13px;
      white-space: pre-wrap;
      word-break: break-word;
      margin: 0;
    }
  `]
})
export class AuditLogDetailComponent {
  details = input.required<Record<string, unknown>>();
}
```

5. **Create `AuditLogViewerComponent`** as the main page:

```typescript
// client/src/app/features/admin/audit-log/
//   audit-log-viewer.component.ts
import {
  Component, OnInit, signal, inject
} from '@angular/core';
import { MatTableModule } from
  '@angular/material/table';
import { MatPaginatorModule, PageEvent } from
  '@angular/material/paginator';
import { MatProgressBarModule } from
  '@angular/material/progress-bar';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { AuditLogFilterComponent } from
  './audit-log-filter.component';
import { AuditLogDetailComponent } from
  './audit-log-detail.component';
import { AuditLogApiService } from
  './audit-log-api.service';
import {
  AuditLogEntry, AuditLogQueryParams
} from './models/audit-log.models';

@Component({
  selector: 'app-audit-log-viewer',
  standalone: true,
  imports: [
    MatTableModule, MatPaginatorModule,
    MatProgressBarModule, MatButtonModule,
    MatIconModule, DatePipe,
    AuditLogFilterComponent,
    AuditLogDetailComponent
  ],
  templateUrl: './audit-log-viewer.component.html',
  styleUrl: './audit-log-viewer.component.scss'
})
export class AuditLogViewerComponent
    implements OnInit {
  private readonly api = inject(AuditLogApiService);
  private readonly snackBar = inject(MatSnackBar);

  readonly entries = signal<AuditLogEntry[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly exporting = signal(false);
  readonly expandedRow =
    signal<string | null>(null);

  readonly displayedColumns = [
    'createdAt', 'actorName', 'eventType',
    'entityType', 'entityId'
  ];

  private currentParams: AuditLogQueryParams = {
    page: 1, pageSize: 25
  };

  ngOnInit(): void {
    this.loadData();
  }

  onFiltersChanged(
    params: AuditLogQueryParams
  ): void {
    this.currentParams = {
      ...params,
      page: 1,
      pageSize: this.currentParams.pageSize
    };
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.currentParams = {
      ...this.currentParams,
      page: event.pageIndex + 1,
      pageSize: event.pageSize
    };
    this.loadData();
  }

  toggleRow(auditId: string): void {
    this.expandedRow.set(
      this.expandedRow() === auditId
        ? null : auditId);
  }

  exportCsv(): void {
    this.exporting.set(true);
    this.api.triggerExport(this.currentParams)
      .subscribe({
        next: (job) => this.pollExport(job.jobId),
        error: () => {
          this.exporting.set(false);
          this.snackBar.open(
            'Export failed', 'Dismiss',
            { duration: 5000 });
        }
      });
  }

  retryLoad(): void {
    this.errorMessage.set('');
    this.loadData();
  }

  private loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.api.query(this.currentParams).subscribe({
      next: (result) => {
        this.entries.set(result.items);
        this.totalCount.set(result.total);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set(
          'Failed to load audit logs');
        this.loading.set(false);
      }
    });
  }

  private pollExport(jobId: string): void {
    const interval = setInterval(() => {
      this.api.getExportStatus(jobId).subscribe({
        next: (status) => {
          if (status.status === 'Complete') {
            clearInterval(interval);
            this.exporting.set(false);
            // Trigger file download
            window.open(
              `/api/v1/admin/audit-logs` +
              `/export/${jobId}`, '_blank');
            this.snackBar.open(
              'Export ready', 'Dismiss',
              { duration: 3000 });
          }
        },
        error: () => {
          clearInterval(interval);
          this.exporting.set(false);
          this.snackBar.open(
            'Export failed', 'Dismiss',
            { duration: 5000 });
        }
      });
    }, 3000);
  }
}
```

```html
<!-- audit-log-viewer.component.html -->
<div class="audit-log-container">
  <h1>Audit Log Viewer</h1>

  <!-- Filter bar -->
  <app-audit-log-filter
    (filtersChanged)="onFiltersChanged($event)">
  </app-audit-log-filter>

  <!-- Export button -->
  <div class="actions-bar">
    <button mat-raised-button
            (click)="exportCsv()"
            [disabled]="exporting()">
      @if (exporting()) {
        <mat-icon>hourglass_empty</mat-icon>
        Exporting...
      } @else {
        <mat-icon>download</mat-icon>
        Export CSV
      }
    </button>
  </div>

  <!-- Loading state: skeleton rows -->
  @if (loading()) {
    <mat-progress-bar mode="indeterminate"
      aria-label="Loading audit logs">
    </mat-progress-bar>
  }

  <!-- Error state: retry banner -->
  @if (errorMessage()) {
    <div class="error-banner" role="alert">
      <span>{{ errorMessage() }}</span>
      <button mat-button
              color="primary"
              (click)="retryLoad()">
        Retry
      </button>
    </div>
  }

  <!-- Empty state -->
  @if (!loading() && !errorMessage()
       && entries().length === 0) {
    <div class="empty-state" role="status">
      <mat-icon class="empty-icon">
        search_off
      </mat-icon>
      <p>No audit events match filters</p>
    </div>
  }

  <!-- Default state: data table -->
  @if (!loading() && !errorMessage()
       && entries().length > 0) {
    <!-- Desktop table (>= 768px) -->
    <div class="table-container desktop-only">
      <table mat-table [dataSource]="entries()"
             aria-label="Audit log entries">
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef>
            Timestamp
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.createdAt | date:'short' }}
          </td>
        </ng-container>

        <ng-container matColumnDef="actorName">
          <th mat-header-cell *matHeaderCellDef>
            Actor
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.actorName }}
          </td>
        </ng-container>

        <ng-container matColumnDef="eventType">
          <th mat-header-cell *matHeaderCellDef>
            Event Type
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.eventType }}
          </td>
        </ng-container>

        <ng-container matColumnDef="entityType">
          <th mat-header-cell *matHeaderCellDef>
            Entity
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.entityType }}
          </td>
        </ng-container>

        <ng-container matColumnDef="entityId">
          <th mat-header-cell *matHeaderCellDef>
            Resource ID
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.entityId || '—' }}
          </td>
        </ng-container>

        <tr mat-header-row
            *matHeaderRowDef="displayedColumns">
        </tr>
        <tr mat-row
            *matRowDef="let row;
                        columns: displayedColumns"
            (click)="toggleRow(row.auditId)"
            [class.expanded]="
              expandedRow() === row.auditId"
            tabindex="0"
            (keyup.enter)="toggleRow(row.auditId)"
            [attr.aria-expanded]="
              expandedRow() === row.auditId">
        </tr>
      </table>

      <!-- Inline detail expansion -->
      @for (entry of entries(); track entry.auditId) {
        @if (expandedRow() === entry.auditId) {
          <app-audit-log-detail
            [details]="entry.details">
          </app-audit-log-detail>
        }
      }
    </div>

    <!-- Mobile cards (< 768px) per UXR-303 -->
    <div class="card-container mobile-only">
      @for (entry of entries(); track entry.auditId) {
        <div class="audit-card"
             (click)="toggleRow(entry.auditId)"
             tabindex="0"
             (keyup.enter)="
               toggleRow(entry.auditId)"
             role="button"
             [attr.aria-expanded]="
               expandedRow() === entry.auditId">
          <div class="card-header">
            <span class="event-type">
              {{ entry.eventType }}
            </span>
            <span class="timestamp">
              {{ entry.createdAt | date:'short' }}
            </span>
          </div>
          <div class="card-body">
            <div class="card-field">
              <span class="label">Actor</span>
              <span>{{ entry.actorName }}</span>
            </div>
            <div class="card-field">
              <span class="label">Entity</span>
              <span>{{ entry.entityType }}</span>
            </div>
            @if (entry.entityId) {
              <div class="card-field">
                <span class="label">Resource</span>
                <span>{{ entry.entityId }}</span>
              </div>
            }
          </div>
          @if (expandedRow() === entry.auditId) {
            <app-audit-log-detail
              [details]="entry.details">
            </app-audit-log-detail>
          }
        </div>
      }
    </div>

    <!-- Pagination (UXR-303) -->
    <mat-paginator
      [length]="totalCount()"
      [pageSize]="25"
      [pageSizeOptions]="[10, 25, 50, 100]"
      (page)="onPageChange($event)"
      aria-label="Audit log pagination">
    </mat-paginator>
  }
</div>
```

```scss
// audit-log-viewer.component.scss

.audit-log-container {
  padding: 24px;
  max-width: 1440px;
  margin: 0 auto;
}

h1 {
  margin-bottom: 24px;
  font-size: 24px;
  font-weight: 500;
}

.actions-bar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 16px;
}

.error-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: #FFEBEE;
  border-radius: 4px;
  margin-bottom: 16px;
  color: #B71C1C;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 64px 0;
  color: #757575;

  .empty-icon {
    font-size: 48px;
    width: 48px;
    height: 48px;
    margin-bottom: 16px;
  }
}

.table-container {
  overflow-x: auto;
}

table {
  width: 100%;
}

tr.mat-mdc-row {
  cursor: pointer;

  &:hover {
    background: #F5F5F5;
  }

  &.expanded {
    background: #E3F2FD;
  }
}

// UXR-303: Responsive breakpoints
.desktop-only {
  display: block;
}

.mobile-only {
  display: none;
}

@media (max-width: 767px) {
  .audit-log-container {
    padding: 16px 8px;
  }

  .desktop-only {
    display: none;
  }

  .mobile-only {
    display: flex;
    flex-direction: column;
    gap: 12px;
  }

  .audit-card {
    border: 1px solid #E0E0E0;
    border-radius: 8px;
    padding: 12px;
    cursor: pointer;

    .card-header {
      display: flex;
      justify-content: space-between;
      margin-bottom: 8px;

      .event-type {
        font-weight: 600;
      }

      .timestamp {
        color: #757575;
        font-size: 13px;
      }
    }

    .card-body {
      display: flex;
      flex-direction: column;
      gap: 4px;

      .card-field {
        display: flex;
        gap: 8px;

        .label {
          font-weight: 500;
          color: #616161;
          min-width: 70px;
        }
      }
    }
  }
}
```

6. **Add lazy-loaded route** with admin guard:

```typescript
// In app.routes.ts
{
  path: 'admin/audit-logs',
  loadComponent: () =>
    import(
      './features/admin/audit-log/' +
      'audit-log-viewer.component'
    ).then(m => m.AuditLogViewerComponent),
  canActivate: [adminGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts                              (modify)
            └── features/
                └── admin/
                    └── audit-log/
                        ├── audit-log-viewer.component.ts    (new)
                        ├── audit-log-viewer.component.html  (new)
                        ├── audit-log-viewer.component.scss  (new)
                        ├── audit-log-filter.component.ts    (new)
                        ├── audit-log-filter.component.html  (new)
                        ├── audit-log-detail.component.ts    (new)
                        ├── audit-log-api.service.ts         (new)
                        └── models/
                            └── audit-log.models.ts          (new)
```

> Placeholder: Update on execution based on US_056 task_001 and US_015 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/admin/audit-log/models/audit-log.models.ts | Interfaces for audit log entries, query params, export job, event types |
| CREATE | client/src/app/features/admin/audit-log/audit-log-api.service.ts | HttpClient service for query, export trigger, and export status polling |
| CREATE | client/src/app/features/admin/audit-log/audit-log-filter.component.ts | Filter bar with event type, actor, date range, resource ID, removable chips |
| CREATE | client/src/app/features/admin/audit-log/audit-log-filter.component.html | Filter bar template with Material form fields and chip set |
| CREATE | client/src/app/features/admin/audit-log/audit-log-detail.component.ts | Inline JSON detail viewer for expanded audit event rows |
| CREATE | client/src/app/features/admin/audit-log/audit-log-viewer.component.ts | Main page with data table, pagination, 5 states, export trigger |
| CREATE | client/src/app/features/admin/audit-log/audit-log-viewer.component.html | Template with desktop table, mobile cards, filter bar, pagination |
| CREATE | client/src/app/features/admin/audit-log/audit-log-viewer.component.scss | Responsive styles with table/card breakpoint at 768px |
| MODIFY | client/src/app/app.routes.ts | Add /admin/audit-logs lazy-loaded route with admin guard |

## External References

- Angular Material Table: https://material.angular.io/components/table/overview
- Angular Material Paginator: https://material.angular.io/components/paginator/overview
- Angular Material Chips: https://material.angular.io/components/chips/overview
- Angular Material Datepicker: https://material.angular.io/components/datepicker/overview
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

# Test audit log viewer:
# 1. Log in as Admin user
# 2. Navigate to /admin/audit-logs
# 3. Apply filters, verify pagination, expand rows
# 4. Test export CSV flow
# 5. Resize to < 768px, verify card layout
```

## Implementation Validation Strategy

- [ ] Admin-only route guard restricts access to Admin role users
- [ ] Filter bar supports event type, actor, date range, and resource ID filters (AC-4)
- [ ] Applied filters render as removable chips (SCR-021 Validation state)
- [ ] Data table displays paginated results within 3 seconds (AC-4)
- [ ] Row click expands inline detail showing full JSON payload
- [ ] Data table switches to card layout below 768px (UXR-303)
- [ ] Export button triggers async export and provides download link (edge case 2)
- [ ] All 5 states rendered: Default, Loading, Empty, Error, Validation (SCR-021)
- [ ] Text meets WCAG AA 4.5:1 contrast ratio (UXR-201)
- [ ] All interactive elements keyboard navigable (UXR-202)

## Implementation Checklist

- [ ] Create TypeScript interfaces for audit log entries, query params, and export models
- [ ] Implement AuditLogApiService with query, export trigger, and export status polling
- [ ] Build AuditLogFilterComponent with event type, actor, date range, resource ID controls
- [ ] Implement removable filter chips for active filters (SCR-021 Validation state)
- [ ] Build AuditLogViewerComponent with data table, inline expansion, and 5 states
- [ ] Implement responsive card layout for screens below 768px (UXR-303)
- [ ] Implement CSV export trigger with polling and secure download (edge case 2)
- [ ] Add /admin/audit-logs lazy-loaded route with admin guard
