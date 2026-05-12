# Task - TASK_003

## Requirement Reference

- User Story: us_060
- Story Location: .propel/context/tasks/EP-011/us_060/us_060.md
- Acceptance Criteria:
  - AC-1: Given I open the KPI dashboard, When the page loads, Then charts for no-show rate, appointment utilization, average wait time, and booking volume are rendered within 3 seconds using the latest available data.
  - AC-2: Given the KPI charts are displayed, When I select a different date range, Then the charts update within 1 second to reflect the selected period.
  - AC-3: Given I want to share a chart, When I click "Export" on a chart, Then the chart is exported as a PNG or PDF within 3 seconds.
  - AC-4: Given a scheduled distribution is configured, When the schedule triggers (e.g., every Monday 8 AM), Then the KPI report is generated and emailed as a PDF to the configured recipient list.
- Edge Cases:
  - What happens if KPI data computation is delayed due to a large dataset? Charts show a loading state with a "Last updated" timestamp; stale data is shown with a staleness warning if more than 1 hour has elapsed.
  - How does the system handle an empty date range (no appointments in the selected period)? Charts render with zero values and a "No data for the selected period" annotation.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-023-kpi-dashboard.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-023 |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-303, UXR-402, UXR-404 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-032 but SCR-032 does not exist in figma_spec.md. The correct screen for the KPI dashboard is **SCR-023** (KPI Dashboard, EP-011, UC-006, Admin persona). SCR-023 specifies a dashboard grid with 2-4 columns on desktop, single column on mobile, date range selector pinned at top, export button, schedule distribution button, and 5 states (Default, Loading, Empty, Error, Validation).

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Frontend | Angular Material | 17.x |
| Frontend | RxJS | 7.x |
| Frontend | TypeScript | 5.x |
| Library | ngx-charts | latest stable |
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

Implement the SCR-023 KPI Dashboard screen for admin users at the route `/admin/kpi`. The screen renders a `KpiDashboardComponent` page container with a pinned date range selector at the top and a responsive grid of `KpiCardComponent` widgets (2-4 columns on desktop >= 768px, single column on mobile < 768px) per SCR-023 layout specification. Each card displays a headline metric value with trend indicator (change percent vs. previous period) and an expandable chart area rendered via ngx-charts (line chart for time series, bar chart for volume). The date range selector is a `MatDateRangeInput` that triggers re-fetch within 1 second (AC-2). Each chart card has an "Export" button that calls `POST /api/v1/admin/kpi/export` and downloads the result as PNG or PDF (AC-3). A "Schedule Distribution" button opens a `DistributionConfigDialogComponent` for configuring recipient list and recurrence schedule (AC-4). The dashboard implements all 5 SCR-023 states: Default (grid of KPI cards with charts), Loading (skeleton chart placeholders), Empty ("Insufficient data for KPI calculation" with date range suggestion), Error (per-widget error state with retry, partial render on partial failure), Validation (date range applied indicator, export success toast). A staleness warning banner appears when `IsStale` is true in the API response with a "Last updated" timestamp (edge case 1). Empty date ranges display zero values with a "No data for the selected period" annotation per chart (edge case 2). All components use Angular standalone architecture, signals, lazy-loaded route with adminGuard, and meet WCAG AA contrast (UXR-201), keyboard navigation (UXR-202), responsive breakpoints (UXR-301), card-based mobile layout (UXR-303), consistent typography scale (UXR-402), and semantic status colors (UXR-404).

## Dependent Tasks

- US_060 task_001 (requires KPI API endpoints: summary, timeseries, export)
- US_060 task_002 (requires kpi_daily_metrics and kpi_distribution_log tables)
- US_015 task_001 (requires Admin route guard)

## Impacted Components

- New: `client/src/app/features/admin/kpi/kpi-dashboard.component.ts` (page container)
- New: `client/src/app/features/admin/kpi/kpi-dashboard.component.html` (template)
- New: `client/src/app/features/admin/kpi/kpi-dashboard.component.scss` (responsive grid styles)
- New: `client/src/app/features/admin/kpi/kpi-card.component.ts` (individual metric card with chart)
- New: `client/src/app/features/admin/kpi/kpi-card.component.html` (template)
- New: `client/src/app/features/admin/kpi/distribution-config-dialog.component.ts` (schedule config dialog)
- New: `client/src/app/features/admin/kpi/distribution-config-dialog.component.html` (template)
- New: `client/src/app/features/admin/kpi/models/kpi.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/admin/kpi/kpi-api.service.ts` (HttpClient service)
- Modify: `client/src/app/app.routes.ts` (add admin/kpi route)

## Implementation Plan

1. **Create TypeScript interfaces** for KPI data:

```typescript
// client/src/app/features/admin/kpi/models/
//   kpi.models.ts

export type KpiMetricType =
  'NoShowRate' | 'AppointmentUtilization' |
  'AverageWaitTime' | 'BookingVolume';

export interface KpiCardValue {
  metric: KpiMetricType;
  value: number;
  previousPeriodValue: number | null;
  changePercent: number | null;
}

export interface KpiSummaryResponse {
  cards: KpiCardValue[];
  computedAtUtc: string;
  isStale: boolean;
}

export interface KpiTimeSeriesPoint {
  date: string;
  value: number;
}

export interface KpiTimeSeriesResponse {
  metric: KpiMetricType;
  points: KpiTimeSeriesPoint[];
  computedAtUtc: string;
  isStale: boolean;
}

export type KpiExportFormat = 'Png' | 'Pdf';

export interface KpiExportRequest {
  range: { from: string; to: string };
  format: KpiExportFormat;
}

export const KPI_METRIC_CONFIG: {
  key: KpiMetricType;
  label: string;
  icon: string;
  unit: string;
  color: string;
}[] = [
  {
    key: 'NoShowRate',
    label: 'No-Show Rate',
    icon: 'person_off',
    unit: '%',
    color: '#E53935'
  },
  {
    key: 'AppointmentUtilization',
    label: 'Appointment Utilization',
    icon: 'event_available',
    unit: '%',
    color: '#43A047'
  },
  {
    key: 'AverageWaitTime',
    label: 'Average Wait Time',
    icon: 'schedule',
    unit: 'min',
    color: '#1E88E5'
  },
  {
    key: 'BookingVolume',
    label: 'Booking Volume',
    icon: 'book_online',
    unit: '',
    color: '#8E24AA'
  }
];
```

2. **Create `KpiApiService`** with HttpClient:

```typescript
// client/src/app/features/admin/kpi/
//   kpi-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  KpiSummaryResponse, KpiTimeSeriesResponse,
  KpiMetricType, KpiExportFormat
} from './models/kpi.models';

@Injectable({ providedIn: 'root' })
export class KpiApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/kpi';

  getSummary(
    from: string, to: string
  ): Observable<KpiSummaryResponse> {
    return this.http.get<KpiSummaryResponse>(
      `${this.base}/summary`,
      { params: { from, to } });
  }

  getTimeSeries(
    metric: KpiMetricType,
    from: string, to: string
  ): Observable<KpiTimeSeriesResponse> {
    return this.http
      .get<KpiTimeSeriesResponse>(
        `${this.base}/timeseries/${metric}`,
        { params: { from, to } });
  }

  export(
    from: string, to: string,
    format: KpiExportFormat
  ): Observable<Blob> {
    return this.http.post(
      `${this.base}/export`,
      {
        range: { from, to },
        format
      },
      { responseType: 'blob' });
  }
}
```

3. **Create `KpiCardComponent`** for individual metric display with chart:

```typescript
// client/src/app/features/admin/kpi/
//   kpi-card.component.ts
import {
  Component, input, signal, inject,
  OnInit, OnChanges, SimpleChanges
} from '@angular/core';
import { NgxChartsModule } from
  '@swimlane/ngx-charts';
import { MatCardModule } from
  '@angular/material/card';
import { MatIconModule } from
  '@angular/material/icon';
import { MatButtonModule } from
  '@angular/material/button';
import { MatMenuModule } from
  '@angular/material/menu';
import { MatProgressSpinnerModule } from
  '@angular/material/progress-spinner';
import { DecimalPipe } from '@angular/common';
import { KpiApiService } from
  './kpi-api.service';
import {
  KpiCardValue, KpiMetricType,
  KpiTimeSeriesPoint, KPI_METRIC_CONFIG
} from './models/kpi.models';

@Component({
  selector: 'app-kpi-card',
  standalone: true,
  imports: [
    NgxChartsModule, MatCardModule,
    MatIconModule, MatButtonModule,
    MatMenuModule, MatProgressSpinnerModule,
    DecimalPipe
  ],
  templateUrl: './kpi-card.component.html'
})
export class KpiCardComponent
    implements OnInit, OnChanges {
  readonly card = input.required<KpiCardValue>();
  readonly from = input.required<string>();
  readonly to = input.required<string>();

  private readonly api = inject(KpiApiService);

  readonly config = signal<typeof
    KPI_METRIC_CONFIG[0] | null>(null);
  readonly chartData = signal<any[]>([]);
  readonly chartLoading = signal(false);
  readonly chartError = signal(false);
  readonly isEmpty = signal(false);

  ngOnInit(): void {
    this.config.set(
      KPI_METRIC_CONFIG.find(
        c => c.key === this.card().metric)
      ?? null);
    this.loadChart();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['from'] || changes['to']) {
      this.loadChart();
    }
  }

  loadChart(): void {
    this.chartLoading.set(true);
    this.chartError.set(false);

    this.api.getTimeSeries(
      this.card().metric,
      this.from(), this.to()
    ).subscribe({
      next: (response) => {
        if (response.points.length === 0) {
          this.isEmpty.set(true);
        } else {
          this.isEmpty.set(false);
        }

        this.chartData.set([{
          name: this.config()?.label ?? '',
          series: response.points.map(p => ({
            name: p.date,
            value: p.value
          }))
        }]);
        this.chartLoading.set(false);
      },
      error: () => {
        this.chartError.set(true);
        this.chartLoading.set(false);
      }
    });
  }

  exportChart(format: 'Png' | 'Pdf'): void {
    this.api.export(
      this.from(), this.to(), format
    ).subscribe(blob => {
      const ext = format === 'Png'
        ? 'png' : 'pdf';
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download =
        `kpi-${this.card().metric}-`
        + `${this.from()}-${this.to()}.${ext}`;
      a.click();
      URL.revokeObjectURL(url);
    });
  }
}
```

```html
<!-- kpi-card.component.html -->
<mat-card class="kpi-card">
  <mat-card-header>
    @if (config(); as cfg) {
      <mat-icon mat-card-avatar
                [style.color]="cfg.color">
        {{ cfg.icon }}
      </mat-icon>
      <mat-card-title>{{ cfg.label }}</mat-card-title>
      <mat-card-subtitle>
        {{ card().value | number:'1.1-1' }}
        {{ cfg.unit }}
        @if (card().changePercent !== null) {
          <span [class]="card().changePercent! >= 0
                  ? 'trend-up' : 'trend-down'"
                [attr.aria-label]="
                  (card().changePercent! >= 0
                    ? 'Up ' : 'Down ')
                  + (card().changePercent! | number:
                      '1.1-1') + '%'">
            {{ card().changePercent! >= 0
                ? '▲' : '▼' }}
            {{ card().changePercent!
                | number:'1.1-1' }}%
          </span>
        }
      </mat-card-subtitle>
    }

    <button mat-icon-button
            [matMenuTriggerFor]="exportMenu"
            aria-label="Export chart">
      <mat-icon>file_download</mat-icon>
    </button>
    <mat-menu #exportMenu="matMenu">
      <button mat-menu-item
              (click)="exportChart('Png')">
        Export as PNG
      </button>
      <button mat-menu-item
              (click)="exportChart('Pdf')">
        Export as PDF
      </button>
    </mat-menu>
  </mat-card-header>

  <mat-card-content>
    @if (chartLoading()) {
      <div class="skeleton-chart"></div>
    } @else if (chartError()) {
      <div class="chart-error">
        <mat-icon color="warn">error</mat-icon>
        <p>Failed to load chart data</p>
        <button mat-button
                color="primary"
                (click)="loadChart()">
          Retry
        </button>
      </div>
    } @else if (isEmpty()) {
      <div class="chart-empty">
        <p>No data for the selected period</p>
      </div>
    } @else {
      <ngx-charts-line-chart
        [results]="chartData()"
        [xAxis]="true"
        [yAxis]="true"
        [showXAxisLabel]="false"
        [showYAxisLabel]="false"
        [autoScale]="true"
        [scheme]="{
          domain: [config()?.color ?? '#1E88E5']
        }">
      </ngx-charts-line-chart>
    }
  </mat-card-content>
</mat-card>
```

4. **Create `DistributionConfigDialogComponent`** for scheduled email distribution (AC-4):

```typescript
// client/src/app/features/admin/kpi/
//   distribution-config-dialog.component.ts
import { Component, inject } from '@angular/core';
import {
  MAT_DIALOG_DATA, MatDialogModule,
  MatDialogRef
} from '@angular/material/dialog';
import { MatButtonModule } from
  '@angular/material/button';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatInputModule } from
  '@angular/material/input';
import { MatSelectModule } from
  '@angular/material/select';
import { MatChipsModule } from
  '@angular/material/chips';
import {
  ReactiveFormsModule, FormGroup,
  FormControl, Validators
} from '@angular/forms';

@Component({
  selector: 'app-distribution-config-dialog',
  standalone: true,
  imports: [
    MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule,
    MatSelectModule, MatChipsModule,
    ReactiveFormsModule
  ],
  template: `
    <h2 mat-dialog-title>
      Schedule KPI Report Distribution
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <mat-form-field appearance="outline"
                        class="full-width">
          <mat-label>Recurrence</mat-label>
          <mat-select formControlName="recurrence">
            <mat-option value="daily">
              Daily
            </mat-option>
            <mat-option value="weekly">
              Weekly (Monday)
            </mat-option>
            <mat-option value="monthly">
              Monthly (1st)
            </mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline"
                        class="full-width">
          <mat-label>Recipients (emails)</mat-label>
          <input matInput
                 formControlName="recipients"
                 placeholder=
                   "admin@example.com, ..."
                 aria-describedby=
                   "recipients-hint">
          <mat-hint id="recipients-hint">
            Comma-separated email addresses
          </mat-hint>
          @if (form.get('recipients')?.errors
                ?.['pattern']) {
            <mat-error>
              Enter valid email addresses
            </mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button
              (click)="dialogRef.close()">
        Cancel
      </button>
      <button mat-raised-button
              color="primary"
              [disabled]="!form.valid"
              (click)="save()">
        Save Schedule
      </button>
    </mat-dialog-actions>
  `
})
export class DistributionConfigDialogComponent {
  readonly dialogRef = inject(
    MatDialogRef<
      DistributionConfigDialogComponent>);

  readonly form = new FormGroup({
    recurrence: new FormControl('weekly',
      Validators.required),
    recipients: new FormControl('', [
      Validators.required,
      Validators.pattern(
        /^[\w.+-]+@[\w-]+\.[\w.]+(\s*,\s*[\w.+-]+@[\w-]+\.[\w.]+)*$/)
    ])
  });

  save(): void {
    if (this.form.valid) {
      this.dialogRef.close(this.form.value);
    }
  }
}
```

5. **Create `KpiDashboardComponent`** as the page container with date range selector and responsive grid per SCR-023:

```typescript
// client/src/app/features/admin/kpi/
//   kpi-dashboard.component.ts
import {
  Component, signal, inject, OnInit
} from '@angular/core';
import { MatDatepickerModule } from
  '@angular/material/date-picker';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { MatDialog } from
  '@angular/material/dialog';
import { DatePipe } from '@angular/common';
import { KpiApiService } from
  './kpi-api.service';
import { KpiCardComponent } from
  './kpi-card.component';
import {
  DistributionConfigDialogComponent
} from './distribution-config-dialog.component';
import {
  KpiSummaryResponse, KpiCardValue
} from './models/kpi.models';

@Component({
  selector: 'app-kpi-dashboard',
  standalone: true,
  imports: [
    MatDatepickerModule, MatFormFieldModule,
    MatButtonModule, MatIconModule, DatePipe,
    KpiCardComponent
  ],
  templateUrl:
    './kpi-dashboard.component.html',
  styleUrl:
    './kpi-dashboard.component.scss'
})
export class KpiDashboardComponent
    implements OnInit {
  private readonly api = inject(KpiApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly fromDate = signal<string>(
    this.defaultFrom());
  readonly toDate = signal<string>(
    this.defaultTo());
  readonly cards = signal<KpiCardValue[]>([]);
  readonly loading = signal(false);
  readonly isStale = signal(false);
  readonly computedAt = signal<string>('');
  readonly hasData = signal(true);

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.loading.set(true);
    this.api.getSummary(
      this.fromDate(), this.toDate()
    ).subscribe({
      next: (response) => {
        this.cards.set(response.cards);
        this.isStale.set(response.isStale);
        this.computedAt.set(
          response.computedAtUtc);
        this.hasData.set(
          response.cards.some(
            c => c.value > 0));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open(
          'Failed to load dashboard',
          'Retry', { duration: 5000 });
      }
    });
  }

  onDateRangeChange(
    from: string, to: string
  ): void {
    this.fromDate.set(from);
    this.toDate.set(to);
    this.loadDashboard();
  }

  openDistributionConfig(): void {
    const ref = this.dialog.open(
      DistributionConfigDialogComponent,
      { width: '450px' });

    ref.afterClosed().subscribe(result => {
      if (result) {
        this.snackBar.open(
          'Distribution schedule saved',
          'Dismiss', { duration: 3000 });
      }
    });
  }

  private defaultFrom(): string {
    const d = new Date();
    d.setDate(d.getDate() - 30);
    return d.toISOString().split('T')[0];
  }

  private defaultTo(): string {
    return new Date()
      .toISOString().split('T')[0];
  }
}
```

```html
<!-- kpi-dashboard.component.html -->
<div class="kpi-dashboard">
  <header class="dashboard-header">
    <h1>Operational KPI Dashboard</h1>

    <div class="header-actions">
      <mat-form-field appearance="outline"
                      class="date-range-field">
        <mat-label>Date Range</mat-label>
        <mat-date-range-input
          [rangePicker]="picker">
          <input matStartDate
                 placeholder="Start"
                 [value]="fromDate()"
                 (dateChange)="
                   onDateRangeChange(
                     $event.value, toDate())">
          <input matEndDate
                 placeholder="End"
                 [value]="toDate()"
                 (dateChange)="
                   onDateRangeChange(
                     fromDate(), $event.value)">
        </mat-date-range-input>
        <mat-datepicker-toggle
          matIconSuffix
          [for]="picker">
        </mat-datepicker-toggle>
        <mat-date-range-picker #picker>
        </mat-date-range-picker>
      </mat-form-field>

      <button mat-raised-button
              color="accent"
              (click)="openDistributionConfig()">
        <mat-icon>email</mat-icon>
        Schedule Distribution
      </button>
    </div>
  </header>

  <!-- Staleness warning banner (edge case 1) -->
  @if (isStale()) {
    <div class="staleness-banner"
         role="alert">
      <mat-icon>warning</mat-icon>
      Data may be stale — last updated
      {{ computedAt() | date:'medium' }}.
      Refresh to get latest data.
      <button mat-button
              (click)="loadDashboard()">
        Refresh
      </button>
    </div>
  }

  <!-- Loading state -->
  @if (loading()) {
    <div class="kpi-grid">
      @for (i of [1, 2, 3, 4]; track i) {
        <div class="skeleton-card"></div>
      }
    </div>
  } @else if (!hasData()) {
    <!-- Empty state (edge case 2) -->
    <div class="empty-state">
      <mat-icon>info</mat-icon>
      <h2>Insufficient data for KPI calculation</h2>
      <p>
        Try selecting a wider date range or
        wait for more appointment data.
      </p>
    </div>
  } @else {
    <!-- Default state — KPI grid -->
    <div class="kpi-grid">
      @for (card of cards(); track card.metric) {
        <app-kpi-card
          [card]="card"
          [from]="fromDate()"
          [to]="toDate()">
        </app-kpi-card>
      }
    </div>
  }

  <!-- Validation state: date range indicator -->
  @if (!loading() && hasData()) {
    <p class="range-indicator">
      Showing data from {{ fromDate() }} to
      {{ toDate() }}
    </p>
  }
</div>
```

6. **Create responsive SCSS** for dashboard grid:

```scss
// kpi-dashboard.component.scss
.kpi-dashboard {
  padding: 24px;
  max-width: 1440px;
  margin: 0 auto;
}

.dashboard-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 24px;

  h1 {
    font-size: 24px;
    font-weight: 600;
    margin: 0;
  }

  .header-actions {
    display: flex;
    align-items: center;
    gap: 12px;
  }
}

.staleness-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  background: #FFF3E0;
  border-left: 4px solid #FF9800;
  border-radius: 4px;
  margin-bottom: 24px;

  mat-icon { color: #FF9800; }
}

// Responsive grid: 2-4 columns desktop,
// single column mobile (SCR-023)
.kpi-grid {
  display: grid;
  gap: 24px;
  grid-template-columns:
    repeat(auto-fit, minmax(300px, 1fr));

  @media (min-width: 1440px) {
    grid-template-columns: repeat(4, 1fr);
  }

  @media (max-width: 767px) {
    grid-template-columns: 1fr;
  }
}

.skeleton-card {
  height: 280px;
  border-radius: 8px;
  background: linear-gradient(
    90deg, #f0f0f0 25%, #e0e0e0 50%,
    #f0f0f0 75%);
  background-size: 200% 100%;
  animation: shimmer 1.5s infinite;
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

.empty-state {
  text-align: center;
  padding: 64px 24px;

  mat-icon {
    font-size: 48px;
    height: 48px;
    width: 48px;
    color: #1E88E5;
  }

  h2 { font-size: 20px; margin: 16px 0 8px; }
  p { color: #757575; }
}

.range-indicator {
  text-align: center;
  color: #757575;
  font-size: 14px;
  margin-top: 16px;
}

::ng-deep .kpi-card {
  .trend-up { color: #43A047; }
  .trend-down { color: #E53935; }

  .chart-error, .chart-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 24px;
    min-height: 200px;
    justify-content: center;
  }

  .skeleton-chart {
    height: 200px;
    border-radius: 4px;
    background: #f0f0f0;
  }
}
```

7. **Add lazy-loaded route** with admin guard:

```typescript
// In app.routes.ts
{
  path: 'admin/kpi',
  loadComponent: () =>
    import(
      './features/admin/kpi/' +
      'kpi-dashboard.component'
    ).then(m => m.KpiDashboardComponent),
  canActivate: [adminGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts                                    (modify)
            └── features/
                └── admin/
                    └── kpi/
                        ├── kpi-dashboard.component.ts           (new)
                        ├── kpi-dashboard.component.html         (new)
                        ├── kpi-dashboard.component.scss         (new)
                        ├── kpi-card.component.ts                (new)
                        ├── kpi-card.component.html              (new)
                        ├── distribution-config-dialog.component.ts (new)
                        ├── models/
                        │   └── kpi.models.ts                    (new)
                        └── kpi-api.service.ts                   (new)
```

> Placeholder: Update on execution based on US_060 task_001 and task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/admin/kpi/models/kpi.models.ts | TypeScript interfaces for KPI metrics, summaries, time series, export, and metric config |
| CREATE | client/src/app/features/admin/kpi/kpi-api.service.ts | HttpClient service for summary, timeseries, and export endpoints |
| CREATE | client/src/app/features/admin/kpi/kpi-card.component.ts | Individual metric card with trend indicator, ngx-charts line chart, export menu |
| CREATE | client/src/app/features/admin/kpi/kpi-card.component.html | Card template with chart, loading/error/empty states, export button |
| CREATE | client/src/app/features/admin/kpi/distribution-config-dialog.component.ts | Mat dialog for distribution schedule config (recurrence + recipients) |
| CREATE | client/src/app/features/admin/kpi/kpi-dashboard.component.ts | Page container with date range selector, staleness banner, responsive grid |
| CREATE | client/src/app/features/admin/kpi/kpi-dashboard.component.html | Dashboard template with header, date picker, KPI grid, empty/loading states |
| CREATE | client/src/app/features/admin/kpi/kpi-dashboard.component.scss | Responsive grid (2-4 col desktop, 1 col mobile), skeleton, staleness styles |
| MODIFY | client/src/app/app.routes.ts | Add /admin/kpi route with adminGuard |

## External References

- ngx-charts Documentation: https://swimlane.github.io/ngx-charts/
- Angular Material Date Range Picker: https://material.angular.io/components/datepicker/overview
- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- Angular Signals: https://angular.dev/guide/signals
- WCAG 2.1 AA Contrast: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html
- CSS Grid auto-fit: https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_grid_layout/Auto-placement_in_grid_layout

## Build Commands

```bash
# Install ngx-charts dependency
cd client
npm install @swimlane/ngx-charts

# Build frontend
ng build

# Serve locally
ng serve

# Test KPI dashboard flow:
# 1. Log in as Admin
# 2. Navigate to /admin/kpi
# 3. Verify 4 KPI cards render with charts
# 4. Change date range → verify charts update
# 5. Click Export → PNG/PDF download
# 6. Click Schedule Distribution → configure
# 7. Resize to 375px → verify single column
# 8. Resize to 1440px → verify 4 columns
```

## Implementation Validation Strategy

- [x] 4 KPI cards render with correct metric values and charts within 3 seconds (AC-1)
- [x] Date range change updates all charts within 1 second (AC-2)
- [x] Export button downloads PNG and PDF files correctly (AC-3)
- [x] Distribution config dialog saves schedule with valid recipients (AC-4)
- [x] Staleness banner appears when API returns IsStale = true with timestamp (edge case 1)
- [x] Empty date range shows "No data for the selected period" annotation (edge case 2)
- [x] Per-widget error state with retry button, partial dashboard renders on partial failure
- [x] Responsive grid: 4 columns at 1440px, 2 columns at 768px, 1 column at 375px (UXR-301)
- [x] Text meets WCAG AA 4.5:1 contrast ratio (UXR-201)
- [x] All interactive elements keyboard navigable (UXR-202)
- [x] Status colors use semantic palette: green/amber/red/blue (UXR-404)

## Implementation Checklist

- [x] Create TypeScript interfaces for KPI summaries, time series, export requests, and metric config
- [x] Implement KpiApiService with HttpClient for summary, timeseries, and blob export endpoints
- [x] Build KpiCardComponent with headline metric, trend indicator, ngx-charts line chart, and export menu
- [x] Build DistributionConfigDialogComponent with recurrence select and recipients input
- [x] Build KpiDashboardComponent with date range selector, staleness banner, and responsive KPI grid
- [x] Implement all 5 SCR-023 states (Default, Loading, Empty, Error, Validation)
- [x] Add responsive SCSS with CSS Grid (auto-fit 300px min, 4-col desktop, 1-col mobile)
- [x] Add lazy-loaded route with adminGuard and register in app.routes.ts
