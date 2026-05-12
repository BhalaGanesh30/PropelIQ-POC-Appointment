# Task - TASK_003

## Requirement Reference

- User Story: us_058
- Story Location: .propel/context/tasks/EP-010/us_058/us_058.md
- Acceptance Criteria:
  - AC-1: Given a compliance report schedule is configured, When the scheduled time triggers, Then a HIPAA compliance report is generated covering access log summaries, audit event counts by type, and any detected anomalies for the reporting period.
  - AC-2: Given a compliance report is generated, When I access the reports section, Then the report is available for PDF download with the period, report date, and key metrics clearly labeled.
  - AC-3: Given a distribution list is configured, When a report is generated, Then it is automatically emailed as a PDF attachment to all recipients on the list.
  - AC-4: Given I want an on-demand report, When I trigger manual generation with a selected date range, Then the report is generated and available within 2 minutes for that range.
- Edge Cases:
  - What happens if report generation exceeds 2 minutes for a large date range? An async job is created; user is notified by email when the report is ready; a progress indicator is shown in the UI.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-022-compliance-reports.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-022 |
| **UXR Requirements** | UXR-201, UXR-301 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-030 but SCR-030 is "Application Shell". The correct screen for HIPAA compliance report generation is **SCR-022** (Compliance Reports, EP-010, UC-006, Admin persona). SCR-022 specifies report type selector, date range picker, generate button, report list with download/preview, and 5 states (Default, Loading, Empty, Error, Validation).

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

Implement the SCR-022 Compliance Reports screen for admin users at the route `/admin/compliance-reports`. The screen provides a `ComplianceReportConfigComponent` at the top with a report type selector (`mat-select` with HIPAA report type), date range picker (`mat-date-range-picker`), and a Generate button that triggers on-demand report generation (AC-4). Below the configuration section, a `ComplianceReportListComponent` displays previously generated reports in a `mat-table` with columns: report date, period, report type, status, key metrics summary, and action buttons for download (PDF) and preview (AC-2). When Generate is clicked, the component calls `POST /api/v1/admin/reports`; if the API returns 202 (async job), a `mat-progress-bar` with indeterminate mode is shown and the component polls the status endpoint until completion (edge case 1). A `ScheduleConfigComponent` provides schedule management: recurrence selector (daily/weekly/monthly), day-of-month/week picker, time picker, and active toggle (AC-1). A `DistributionListComponent` manages email recipients per schedule with add/remove/toggle controls (AC-3). The screen implements all 5 states defined in SCR-022: Default (config + report list), Loading (progress bar during generation), Empty ("No reports generated yet" with generate CTA), Error (generation failure alert with retry), Validation (generated report available for preview and download). Single-column layout with config at top and report list below per SCR-022. All components use Angular standalone architecture, signals for state, lazy-loaded route with adminGuard, and meet WCAG AA contrast (UXR-201) and responsive breakpoints (UXR-301).

## Dependent Tasks

- US_058 task_001 (requires compliance report API endpoints: POST generate, GET list, GET download, GET status)
- US_058 task_002 (requires compliance report and schedule database schema)
- US_015 task_001 (requires Admin route guard)

## Impacted Components

- New: `client/src/app/features/admin/compliance/compliance-reports.component.ts` (page container)
- New: `client/src/app/features/admin/compliance/compliance-reports.component.html` (template)
- New: `client/src/app/features/admin/compliance/compliance-reports.component.scss` (styles)
- New: `client/src/app/features/admin/compliance/compliance-report-config.component.ts` (report type + date range + generate)
- New: `client/src/app/features/admin/compliance/compliance-report-config.component.html` (template)
- New: `client/src/app/features/admin/compliance/compliance-report-list.component.ts` (report table)
- New: `client/src/app/features/admin/compliance/compliance-report-list.component.html` (template)
- New: `client/src/app/features/admin/compliance/schedule-config.component.ts` (schedule management)
- New: `client/src/app/features/admin/compliance/schedule-config.component.html` (template)
- New: `client/src/app/features/admin/compliance/distribution-list.component.ts` (recipient management)
- New: `client/src/app/features/admin/compliance/distribution-list.component.html` (template)
- New: `client/src/app/features/admin/compliance/models/compliance.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/admin/compliance/compliance-api.service.ts` (HttpClient service)
- Modify: `client/src/app/app.routes.ts` (add compliance-reports route)

## Implementation Plan

1. **Create TypeScript interfaces** for compliance report data:

```typescript
// client/src/app/features/admin/compliance/models/
//   compliance.models.ts

export type ReportStatus =
  'Pending' | 'Generating' | 'Completed' | 'Failed';

export type RecurrencePattern =
  'Daily' | 'Weekly' | 'Monthly';

export interface ComplianceReport {
  id: string;
  reportType: string;
  periodStartUtc: string;
  periodEndUtc: string;
  status: ReportStatus;
  generatedAtUtc: string;
  totalAuditEvents: number;
  uniqueActors: number;
  anomalyCount: number;
}

export interface ComplianceReportPagedResult {
  total: number;
  items: ComplianceReport[];
}

export interface GenerateReportRequest {
  reportType: string;
  periodStartUtc: string;
  periodEndUtc: string;
}

export interface GenerateReportResponse {
  id?: string;
  jobId?: string;
  status: string;
}

export interface ReportJobStatus {
  id: string;
  status: string;
  progressPercent: number;
  reportId?: string;
}

export interface ReportSchedule {
  id: string;
  reportType: string;
  recurrencePattern: RecurrencePattern;
  dayOfMonth: number;
  dayOfWeek: number | null;
  scheduledTimeUtc: string;
  nextRunAtUtc: string;
  lastRunAtUtc: string | null;
  isActive: boolean;
  distributionList: DistributionEntry[];
}

export interface DistributionEntry {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
}
```

2. **Create `ComplianceApiService`** for all report HTTP operations:

```typescript
// client/src/app/features/admin/compliance/
//   compliance-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ComplianceReportPagedResult,
  GenerateReportRequest,
  GenerateReportResponse,
  ReportJobStatus,
  ReportSchedule
} from './models/compliance.models';

@Injectable({ providedIn: 'root' })
export class ComplianceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/reports';

  generate(
    req: GenerateReportRequest
  ): Observable<GenerateReportResponse> {
    return this.http
      .post<GenerateReportResponse>(this.base, req);
  }

  list(
    page: number, pageSize: number
  ): Observable<ComplianceReportPagedResult> {
    return this.http
      .get<ComplianceReportPagedResult>(this.base, {
        params: { page, pageSize }
      });
  }

  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(
      `${this.base}/${id}/download`,
      { responseType: 'blob' });
  }

  getJobStatus(
    id: string
  ): Observable<ReportJobStatus> {
    return this.http
      .get<ReportJobStatus>(
        `${this.base}/${id}/status`);
  }

  listSchedules(): Observable<ReportSchedule[]> {
    return this.http
      .get<ReportSchedule[]>(
        `${this.base}/schedules`);
  }

  updateSchedule(
    id: string, schedule: Partial<ReportSchedule>
  ): Observable<ReportSchedule> {
    return this.http
      .put<ReportSchedule>(
        `${this.base}/schedules/${id}`, schedule);
  }
}
```

3. **Create `ComplianceReportConfigComponent`** with report type selector, date range picker, and Generate button (AC-4):

```typescript
// client/src/app/features/admin/compliance/
//   compliance-report-config.component.ts
import {
  Component, signal, inject, output
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatSelectModule } from
  '@angular/material/select';
import { MatDatepickerModule } from
  '@angular/material/datepicker';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatInputModule } from
  '@angular/material/input';
import { MatButtonModule } from
  '@angular/material/button';
import { MatProgressBarModule } from
  '@angular/material/progress-bar';
import { ComplianceApiService } from
  './compliance-api.service';
import { GenerateReportRequest } from
  './models/compliance.models';

@Component({
  selector: 'app-compliance-report-config',
  standalone: true,
  imports: [
    FormsModule, MatSelectModule,
    MatDatepickerModule, MatFormFieldModule,
    MatInputModule, MatButtonModule,
    MatProgressBarModule
  ],
  templateUrl:
    './compliance-report-config.component.html'
})
export class ComplianceReportConfigComponent {
  readonly reportGenerated = output<void>();
  private readonly api =
    inject(ComplianceApiService);

  readonly reportType = signal('HIPAA');
  readonly startDate = signal<Date | null>(null);
  readonly endDate = signal<Date | null>(null);
  readonly generating = signal(false);
  readonly asyncJobId = signal<string | null>(null);
  readonly progress = signal(0);

  generate(): void {
    if (!this.startDate() || !this.endDate()) return;
    this.generating.set(true);

    const req: GenerateReportRequest = {
      reportType: this.reportType(),
      periodStartUtc: this.startDate()!.toISOString(),
      periodEndUtc: this.endDate()!.toISOString()
    };

    this.api.generate(req).subscribe({
      next: (res) => {
        if (res.jobId) {
          // Async job — poll for status
          this.asyncJobId.set(res.jobId);
          this.pollJobStatus(res.jobId);
        } else {
          this.generating.set(false);
          this.reportGenerated.emit();
        }
      },
      error: () => this.generating.set(false)
    });
  }

  private pollJobStatus(jobId: string): void {
    const interval = setInterval(() => {
      this.api.getJobStatus(jobId).subscribe({
        next: (status) => {
          this.progress.set(status.progressPercent);
          if (status.status === 'Completed'
            || status.status === 'Failed') {
            clearInterval(interval);
            this.generating.set(false);
            this.asyncJobId.set(null);
            this.reportGenerated.emit();
          }
        },
        error: () => {
          clearInterval(interval);
          this.generating.set(false);
        }
      });
    }, 5000);
  }
}
```

```html
<!-- compliance-report-config.component.html -->
<div class="report-config">
  <h2>Generate Compliance Report</h2>

  <div class="config-row">
    <mat-form-field appearance="outline">
      <mat-label>Report Type</mat-label>
      <mat-select [(ngModel)]="reportType">
        <mat-option value="HIPAA">
          HIPAA Compliance
        </mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Date Range</mat-label>
      <mat-date-range-input
        [rangePicker]="rangePicker">
        <input matStartDate
               placeholder="Start"
               [(ngModel)]="startDate">
        <input matEndDate
               placeholder="End"
               [(ngModel)]="endDate">
      </mat-date-range-input>
      <mat-datepicker-toggle matIconSuffix
        [for]="rangePicker">
      </mat-datepicker-toggle>
      <mat-date-range-picker #rangePicker>
      </mat-date-range-picker>
    </mat-form-field>

    <button mat-raised-button
            color="primary"
            (click)="generate()"
            [disabled]="generating()
              || !startDate() || !endDate()">
      @if (generating() && !asyncJobId()) {
        Generating...
      } @else {
        Generate Report
      }
    </button>
  </div>

  @if (generating()) {
    <mat-progress-bar
      [mode]="asyncJobId() ? 'determinate'
        : 'indeterminate'"
      [value]="progress()"
      aria-label="Report generation progress">
    </mat-progress-bar>
    @if (asyncJobId()) {
      <p class="progress-text">
        Processing... {{ progress() }}% complete.
        You will be notified by email when ready.
      </p>
    }
  }
</div>
```

4. **Create `ComplianceReportListComponent`** with paginated report table (AC-2):

```typescript
// client/src/app/features/admin/compliance/
//   compliance-report-list.component.ts
import {
  Component, OnInit, signal, inject, input
} from '@angular/core';
import { MatTableModule } from
  '@angular/material/table';
import { MatPaginatorModule, PageEvent } from
  '@angular/material/paginator';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import { MatChipsModule } from
  '@angular/material/chips';
import { DatePipe } from '@angular/common';
import { ComplianceApiService } from
  './compliance-api.service';
import { ComplianceReport } from
  './models/compliance.models';

@Component({
  selector: 'app-compliance-report-list',
  standalone: true,
  imports: [
    MatTableModule, MatPaginatorModule,
    MatButtonModule, MatIconModule,
    MatChipsModule, DatePipe
  ],
  templateUrl:
    './compliance-report-list.component.html'
})
export class ComplianceReportListComponent
    implements OnInit {
  private readonly api =
    inject(ComplianceApiService);

  readonly reports =
    signal<ComplianceReport[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal(false);

  readonly displayedColumns = [
    'generatedAt', 'period', 'reportType',
    'status', 'metrics', 'actions'
  ];

  ngOnInit(): void {
    this.loadReports();
  }

  loadReports(page = 1): void {
    this.loading.set(true);
    this.error.set(false);
    this.api.list(page, 25).subscribe({
      next: (result) => {
        this.reports.set(result.items);
        this.totalCount.set(result.total);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      }
    });
  }

  downloadPdf(report: ComplianceReport): void {
    this.api.downloadPdf(report.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download =
          `compliance-report-${report.id}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.loadReports(event.pageIndex + 1);
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'Completed': return 'primary';
      case 'Failed': return 'warn';
      case 'Generating': return 'accent';
      default: return '';
    }
  }
}
```

```html
<!-- compliance-report-list.component.html -->
<h3>Generated Reports</h3>

@if (loading()) {
  <mat-progress-bar mode="indeterminate">
  </mat-progress-bar>
}

@if (error()) {
  <div class="error-banner" role="alert">
    <p>Failed to load reports.</p>
    <button mat-button color="primary"
            (click)="loadReports()">
      Retry
    </button>
  </div>
} @else if (!loading() && reports().length === 0) {
  <div class="empty-state">
    <p>No reports generated yet.</p>
    <p>Use the form above to generate your first
      compliance report.</p>
  </div>
} @else {
  <table mat-table
         [dataSource]="reports()"
         aria-label="Compliance reports">

    <ng-container matColumnDef="generatedAt">
      <th mat-header-cell *matHeaderCellDef>
        Report Date
      </th>
      <td mat-cell *matCellDef="let r">
        {{ r.generatedAtUtc | date:'short' }}
      </td>
    </ng-container>

    <ng-container matColumnDef="period">
      <th mat-header-cell *matHeaderCellDef>
        Period
      </th>
      <td mat-cell *matCellDef="let r">
        {{ r.periodStartUtc | date:'mediumDate' }} —
        {{ r.periodEndUtc | date:'mediumDate' }}
      </td>
    </ng-container>

    <ng-container matColumnDef="reportType">
      <th mat-header-cell *matHeaderCellDef>
        Type
      </th>
      <td mat-cell *matCellDef="let r">
        {{ r.reportType }}
      </td>
    </ng-container>

    <ng-container matColumnDef="status">
      <th mat-header-cell *matHeaderCellDef>
        Status
      </th>
      <td mat-cell *matCellDef="let r">
        <mat-chip
          [color]="getStatusColor(r.status)">
          {{ r.status }}
        </mat-chip>
      </td>
    </ng-container>

    <ng-container matColumnDef="metrics">
      <th mat-header-cell *matHeaderCellDef>
        Key Metrics
      </th>
      <td mat-cell *matCellDef="let r">
        {{ r.totalAuditEvents }} events,
        {{ r.uniqueActors }} actors,
        {{ r.anomalyCount }} anomalies
      </td>
    </ng-container>

    <ng-container matColumnDef="actions">
      <th mat-header-cell *matHeaderCellDef>
        Actions
      </th>
      <td mat-cell *matCellDef="let r">
        @if (r.status === 'Completed') {
          <button mat-icon-button
                  (click)="downloadPdf(r)"
                  aria-label="Download PDF">
            <mat-icon>download</mat-icon>
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

  <mat-paginator
    [length]="totalCount()"
    [pageSize]="25"
    [pageSizeOptions]="[10, 25, 50]"
    (page)="onPageChange($event)"
    aria-label="Report list pagination">
  </mat-paginator>
}
```

5. **Create `ScheduleConfigComponent`** for recurrence management (AC-1) and `DistributionListComponent` for email recipient management (AC-3). The schedule component provides a `mat-select` for recurrence pattern (Daily, Weekly, Monthly), conditional day-of-month/week picker, `mat-timepicker` for scheduled time, and an `mat-slide-toggle` for active/inactive. The distribution list shows a `mat-list` of recipients with add (dialog) and remove buttons. Both components use signals and call the `ComplianceApiService` for CRUD operations.

6. **Create `ComplianceReportsComponent`** as the page container composing all child components in a single-column layout per SCR-022. Route: `/admin/compliance-reports`, lazy-loaded with adminGuard.

```typescript
// client/src/app/features/admin/compliance/
//   compliance-reports.component.ts
import { Component, ViewChild } from '@angular/core';
import {
  ComplianceReportConfigComponent
} from './compliance-report-config.component';
import {
  ComplianceReportListComponent
} from './compliance-report-list.component';
import {
  ScheduleConfigComponent
} from './schedule-config.component';
import {
  DistributionListComponent
} from './distribution-list.component';
import { MatTabsModule } from
  '@angular/material/tabs';

@Component({
  selector: 'app-compliance-reports',
  standalone: true,
  imports: [
    ComplianceReportConfigComponent,
    ComplianceReportListComponent,
    ScheduleConfigComponent,
    DistributionListComponent,
    MatTabsModule
  ],
  templateUrl:
    './compliance-reports.component.html',
  styleUrl:
    './compliance-reports.component.scss'
})
export class ComplianceReportsComponent {
  @ViewChild(ComplianceReportListComponent)
  reportList!: ComplianceReportListComponent;

  onReportGenerated(): void {
    this.reportList.loadReports();
  }
}
```

```html
<!-- compliance-reports.component.html -->
<div class="compliance-container">
  <h1>Compliance Reports</h1>

  <mat-tab-group>
    <mat-tab label="Generate & Reports">
      <app-compliance-report-config
        (reportGenerated)="onReportGenerated()">
      </app-compliance-report-config>

      <app-compliance-report-list>
      </app-compliance-report-list>
    </mat-tab>

    <mat-tab label="Schedule">
      <app-schedule-config>
      </app-schedule-config>
    </mat-tab>

    <mat-tab label="Distribution">
      <app-distribution-list>
      </app-distribution-list>
    </mat-tab>
  </mat-tab-group>
</div>
```

7. **Add lazy-loaded route** with admin guard:

```typescript
// In app.routes.ts
{
  path: 'admin/compliance-reports',
  loadComponent: () =>
    import(
      './features/admin/compliance/' +
      'compliance-reports.component'
    ).then(m => m.ComplianceReportsComponent),
  canActivate: [adminGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts                                     (modify)
            └── features/
                └── admin/
                    └── compliance/
                        ├── compliance-reports.component.ts        (new)
                        ├── compliance-reports.component.html      (new)
                        ├── compliance-reports.component.scss      (new)
                        ├── compliance-report-config.component.ts  (new)
                        ├── compliance-report-config.component.html (new)
                        ├── compliance-report-list.component.ts    (new)
                        ├── compliance-report-list.component.html  (new)
                        ├── schedule-config.component.ts           (new)
                        ├── schedule-config.component.html         (new)
                        ├── distribution-list.component.ts         (new)
                        ├── distribution-list.component.html       (new)
                        ├── models/
                        │   └── compliance.models.ts               (new)
                        └── compliance-api.service.ts              (new)
```

> Placeholder: Update on execution based on US_058 task_001 and task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/admin/compliance/models/compliance.models.ts | TypeScript interfaces for reports, schedules, distribution |
| CREATE | client/src/app/features/admin/compliance/compliance-api.service.ts | HttpClient service for all compliance report API operations |
| CREATE | client/src/app/features/admin/compliance/compliance-report-config.component.ts | Report type selector, date range picker, generate button with async progress |
| CREATE | client/src/app/features/admin/compliance/compliance-report-config.component.html | Config form template with progress bar |
| CREATE | client/src/app/features/admin/compliance/compliance-report-list.component.ts | Paginated report table with download and status chips |
| CREATE | client/src/app/features/admin/compliance/compliance-report-list.component.html | Table template with 5 SCR-022 states (Default, Loading, Empty, Error, Validation) |
| CREATE | client/src/app/features/admin/compliance/schedule-config.component.ts | Schedule management with recurrence, day, time, and active toggle |
| CREATE | client/src/app/features/admin/compliance/schedule-config.component.html | Schedule configuration form template |
| CREATE | client/src/app/features/admin/compliance/distribution-list.component.ts | Email recipient management with add/remove/toggle |
| CREATE | client/src/app/features/admin/compliance/distribution-list.component.html | Recipient list template with action controls |
| CREATE | client/src/app/features/admin/compliance/compliance-reports.component.ts | Page container with tab group composing child components |
| CREATE | client/src/app/features/admin/compliance/compliance-reports.component.html | Page layout with tabs for Generate, Schedule, Distribution |
| CREATE | client/src/app/features/admin/compliance/compliance-reports.component.scss | Responsive styles for single-column layout |
| MODIFY | client/src/app/app.routes.ts | Add /admin/compliance-reports route with adminGuard |

## External References

- Angular Material Table: https://material.angular.io/components/table/overview
- Angular Material Datepicker (Range): https://material.angular.io/components/datepicker/overview#date-range-selection
- Angular Material Tabs: https://material.angular.io/components/tabs/overview
- Angular Material Progress Bar: https://material.angular.io/components/progress-bar/overview
- Angular Signals: https://angular.dev/guide/signals
- WCAG 2.1 AA Contrast: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Test compliance reports flow:
# 1. Log in as Admin
# 2. Navigate to /admin/compliance-reports
# 3. Select report type and date range
# 4. Click Generate → verify progress bar or PDF
# 5. Download PDF from report list
# 6. Configure schedule in Schedule tab
# 7. Add recipients in Distribution tab
```

## Implementation Validation Strategy

- [x] Report type selector and date range picker render correctly in Default state
- [x] Generate button triggers API call and shows progress bar during Loading state
- [x] Async jobs show determinate progress bar with percentage and email notification text (edge case 1)
- [x] Empty state shows "No reports generated yet" with generate CTA
- [x] Error state shows failure alert with retry button
- [x] Completed reports show download button and key metrics (AC-2)
- [x] Schedule configuration saves recurrence pattern and time (AC-1)
- [x] Distribution list management adds, removes, and toggles recipients (AC-3)
- [x] Text meets WCAG AA 4.5:1 contrast ratio (UXR-201)
- [x] Responsive layout at 375px, 768px, 1440px breakpoints (UXR-301)

## Implementation Checklist

- [x] Create TypeScript interfaces for reports, schedules, distribution entries, and job status
- [x] Implement ComplianceApiService with generate, list, download, status, and schedule operations
- [x] Build ComplianceReportConfigComponent with report type select, date range picker, and async progress
- [x] Build ComplianceReportListComponent with paginated table, status chips, download buttons, and 5 states
- [x] Build ScheduleConfigComponent with recurrence pattern, day, time, and active toggle
- [x] Build DistributionListComponent with recipient add/remove/toggle controls
- [x] Create ComplianceReportsComponent page container with tab layout
- [x] Add lazy-loaded route with adminGuard and register in app.routes.ts
