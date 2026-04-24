# Task - TASK_002

## Requirement Reference

- User Story: us_025
- Story Location: .propel/context/tasks/EP-002/us_025/us_025.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as a patient, When I navigate to "My Appointments," Then a list of all my past and upcoming appointments is displayed sorted by date descending with status labels.
  - AC-2: Given the appointment history is displayed, When I apply a status filter (e.g., Completed, Cancelled, No-Show), Then the list updates within 500 ms to show only appointments matching the filter.
  - AC-3: Given I apply a date range filter, When the filter is applied, Then only appointments within the specified date range are shown.
  - AC-4: Given I click "Export PDF," When the export is processed, Then a PDF containing my filtered appointment history downloads within 5 seconds.
- Edge Cases:
  - What happens if a patient has hundreds of appointments? Pagination is applied with 20 records per page; the export PDF includes all filtered records regardless of pagination.
  - How does the system handle an empty appointment history? An empty state message is displayed: "No appointments found. Book your first appointment."

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-007-appointment-history.html` (pending) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-007 |
| **UXR Requirements** | UXR-201 (typography), UXR-202 (keyboard navigation with visible focus), UXR-301 (responsive breakpoints), UXR-303 (table to card-based layout below 768px), UXR-304 (44x44px touch targets) |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-011 but SCR-011 is "Document Upload" (EP-006). The actual Appointment History screen is **SCR-007** per figma_spec.md, which describes "Paginated list of past and upcoming appointments with date and status filters, PDF export, and reschedule/cancel actions."

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

Enhance the existing Appointment History page (SCR-007, from US_022 task_002) with the appointment history API integration, refined filter controls, server-side pagination, and a PDF export button. The existing page has a mat-table (desktop) and card list (mobile) with a filter bar and cancel dialog. This task replaces the existing data source with the new `GET /api/v1/appointments/history` paginated endpoint (AC-1), adds a `mat-select` status filter with debounced 300ms server round-trip ensuring 500ms total response (AC-2), adds a `mat-date-range-picker` for date range filtering (AC-3), and adds an "Export PDF" button that calls `GET /api/v1/appointments/history/export` as a blob download (AC-4). The paginator is connected to server-side pagination with 20 items per page (edge case). The empty state displays "No appointments found. Book your first appointment." with a booking CTA (edge case, SCR-007 Empty state). All filter changes emit through a `Subject` with `debounceTime(300)` + `switchMap` to cancel in-flight requests. The filter bar and export button support full keyboard navigation with visible focus indicators (UXR-202), and touch targets on mobile are at least 44x44px (UXR-304).

## Dependent Tasks

- US_025 task_001 (requires GET /api/v1/appointments/history and GET /api/v1/appointments/history/export endpoints)
- US_022 task_002 (requires existing AppointmentHistoryComponent with mat-table, card list, cancel dialog)
- US_024 task_002 (requires "Add to Calendar" button already integrated into history page)

## Impacted Components

- Modify: `client/src/app/features/appointments/appointment-history.component.ts` (replace data source with paginated API, add PDF export, debounced filters)
- Modify: `client/src/app/features/appointments/appointment-history.component.html` (add status select, date range picker, export button, paginator)
- Modify: `client/src/app/features/appointments/appointment-history.component.scss` (filter bar layout, export button, touch target sizing)
- New: `client/src/app/features/appointments/appointment-history-api.service.ts` (history and export API client)
- New: `client/src/app/features/appointments/models/appointment-history.models.ts` (TypeScript interfaces)

## Implementation Plan

1. **Create TypeScript interfaces**:

```typescript
// client/src/app/features/appointments/models/appointment-history.models.ts

export interface AppointmentHistoryFilter {
  status?: string;
  dateFrom?: string;
  dateTo?: string;
  page: number;
  pageSize: number;
}

export interface AppointmentHistoryItem {
  id: string;
  appointmentDate: string;
  appointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  status: string;
  providerName: string | null;
  location: string | null;
  confirmationCode: string;
}

export interface AppointmentHistoryResponse {
  items: AppointmentHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const APPOINTMENT_STATUSES = [
  'Confirmed',
  'Completed',
  'Cancelled',
  'No-Show',
  'Rescheduled'
] as const;

export type AppointmentStatus = typeof APPOINTMENT_STATUSES[number];
```

2. **Create `AppointmentHistoryApiService`**:

```typescript
// client/src/app/features/appointments/appointment-history-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AppointmentHistoryFilter,
  AppointmentHistoryResponse
} from './models/appointment-history.models';

@Injectable({ providedIn: 'root' })
export class AppointmentHistoryApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/appointments/history';

  // AC-1, AC-2, AC-3: Paginated filtered history
  getHistory(
    filter: AppointmentHistoryFilter
  ): Observable<AppointmentHistoryResponse> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.status) {
      params = params.set('status', filter.status);
    }
    if (filter.dateFrom) {
      params = params.set('dateFrom', filter.dateFrom);
    }
    if (filter.dateTo) {
      params = params.set('dateTo', filter.dateTo);
    }

    return this.http.get<AppointmentHistoryResponse>(
      this.baseUrl, { params }
    );
  }

  // AC-4: PDF export
  exportPdf(
    filter: AppointmentHistoryFilter
  ): Observable<Blob> {
    let params = new HttpParams();

    if (filter.status) {
      params = params.set('status', filter.status);
    }
    if (filter.dateFrom) {
      params = params.set('dateFrom', filter.dateFrom);
    }
    if (filter.dateTo) {
      params = params.set('dateTo', filter.dateTo);
    }

    return this.http.get(
      `${this.baseUrl}/export`,
      { params, responseType: 'blob' }
    );
  }
}
```

3. **Enhance `AppointmentHistoryComponent`** with server-side pagination and debounced filters:

```typescript
// client/src/app/features/appointments/appointment-history.component.ts
// Enhance existing component

import { Subject, debounceTime, switchMap, takeUntil, tap } from 'rxjs';
import { PageEvent } from '@angular/material/paginator';
import { AppointmentHistoryApiService } from
  './appointment-history-api.service';
import {
  AppointmentHistoryFilter,
  AppointmentHistoryItem,
  APPOINTMENT_STATUSES
} from './models/appointment-history.models';

// Add to component class
private readonly historyApi = inject(AppointmentHistoryApiService);
private readonly filterChange$ = new Subject<void>();
private readonly destroy$ = new Subject<void>();

readonly appointments = signal<AppointmentHistoryItem[]>([]);
readonly totalCount = signal(0);
readonly totalPages = signal(0);
readonly isLoading = signal(true);
readonly isExporting = signal(false);
readonly errorMessage = signal<string | null>(null);
readonly statuses = APPOINTMENT_STATUSES;

// Filter state
currentPage = 1;
pageSize = 20;
selectedStatus: string | undefined;
dateFrom: string | undefined;
dateTo: string | undefined;

ngOnInit(): void {
  // AC-2: Debounced filter with 300ms + switchMap for cancellation
  this.filterChange$.pipe(
    debounceTime(300),
    tap(() => this.isLoading.set(true)),
    switchMap(() => this.historyApi.getHistory(this.buildFilter())),
    takeUntil(this.destroy$)
  ).subscribe({
    next: (response) => {
      this.appointments.set(response.items);
      this.totalCount.set(response.totalCount);
      this.totalPages.set(response.totalPages);
      this.isLoading.set(false);
      this.errorMessage.set(null);
    },
    error: () => {
      this.isLoading.set(false);
      this.errorMessage.set('Failed to load appointment history.');
    }
  });

  // Trigger initial load
  this.filterChange$.next();
}

ngOnDestroy(): void {
  this.destroy$.next();
  this.destroy$.complete();
}

// AC-2: Status filter change
onStatusFilterChange(status: string | undefined): void {
  this.selectedStatus = status;
  this.currentPage = 1;
  this.filterChange$.next();
}

// AC-3: Date range filter change
onDateRangeChange(
  dateFrom: string | undefined,
  dateTo: string | undefined
): void {
  this.dateFrom = dateFrom;
  this.dateTo = dateTo;
  this.currentPage = 1;
  this.filterChange$.next();
}

// Pagination
onPageChange(event: PageEvent): void {
  this.currentPage = event.pageIndex + 1;
  this.pageSize = event.pageSize;
  this.filterChange$.next();
}

// AC-4: Export PDF
exportPdf(): void {
  if (this.isExporting()) return;
  this.isExporting.set(true);

  this.historyApi.exportPdf(this.buildFilter()).subscribe({
    next: (blob) => {
      this.isExporting.set(false);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `appointment-history-${
        new Date().toISOString().split('T')[0]
      }.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);

      this.snackBar.open(
        'PDF exported successfully.', 'Close',
        { duration: 3000 }
      );
    },
    error: () => {
      this.isExporting.set(false);
      this.snackBar.open(
        'Failed to export PDF.', 'Close',
        { duration: 5000 }
      );
    }
  });
}

clearFilters(): void {
  this.selectedStatus = undefined;
  this.dateFrom = undefined;
  this.dateTo = undefined;
  this.currentPage = 1;
  this.filterChange$.next();
}

private buildFilter(): AppointmentHistoryFilter {
  return {
    status: this.selectedStatus,
    dateFrom: this.dateFrom,
    dateTo: this.dateTo,
    page: this.currentPage,
    pageSize: this.pageSize
  };
}
```

4. **Enhance template** with filter bar, export button, and paginator:

```html
<!-- Additions to appointment-history.component.html -->

<!-- Filter bar -->
<div class="filter-bar" role="search" aria-label="Filter appointments">
  <!-- AC-2: Status filter -->
  <mat-form-field appearance="outline" class="filter-field">
    <mat-label>Status</mat-label>
    <mat-select
      [value]="selectedStatus"
      (selectionChange)="onStatusFilterChange($event.value)"
      aria-label="Filter by appointment status">
      <mat-option [value]="undefined">All Statuses</mat-option>
      @for (status of statuses; track status) {
        <mat-option [value]="status">{{ status }}</mat-option>
      }
    </mat-select>
  </mat-form-field>

  <!-- AC-3: Date range filter -->
  <mat-form-field appearance="outline" class="filter-field date-range">
    <mat-label>Date Range</mat-label>
    <mat-date-range-input [rangePicker]="picker">
      <input matStartDate
             placeholder="Start"
             (dateChange)="onDateRangeChange($event.value, dateTo)"
             aria-label="Filter start date">
      <input matEndDate
             placeholder="End"
             (dateChange)="onDateRangeChange(dateFrom, $event.value)"
             aria-label="Filter end date">
    </mat-date-range-input>
    <mat-datepicker-toggle matIconSuffix [for]="picker">
    </mat-datepicker-toggle>
    <mat-date-range-picker #picker></mat-date-range-picker>
  </mat-form-field>

  <!-- Clear filters -->
  <button mat-stroked-button
          (click)="clearFilters()"
          aria-label="Clear all filters">
    <mat-icon>clear</mat-icon> Clear
  </button>

  <!-- AC-4: Export PDF button -->
  <button mat-flat-button
          color="primary"
          [disabled]="isExporting() || appointments().length === 0"
          (click)="exportPdf()"
          aria-label="Export appointment history as PDF">
    @if (isExporting()) {
      <mat-spinner diameter="20"></mat-spinner>
    } @else {
      <mat-icon>picture_as_pdf</mat-icon>
    }
    Export PDF
  </button>
</div>

<!-- Empty state (edge case, SCR-007 Empty) -->
@if (!isLoading() && !errorMessage() && appointments().length === 0) {
  <div class="empty-state" role="status">
    <mat-icon class="empty-icon">event_busy</mat-icon>
    <p>No appointments found. Book your first appointment.</p>
    <button mat-flat-button color="primary"
            (click)="navigateToSearch()"
            aria-label="Book your first appointment">
      Book Appointment
    </button>
  </div>
}

<!-- Paginator (edge case: 20 per page) -->
@if (totalCount() > 0) {
  <mat-paginator
    [length]="totalCount()"
    [pageSize]="pageSize"
    [pageIndex]="currentPage - 1"
    [pageSizeOptions]="[10, 20, 50]"
    (page)="onPageChange($event)"
    aria-label="Appointment history pagination">
  </mat-paginator>
}
```

5. **Enhance styles** with filter layout and touch targets:

```scss
// Additions to appointment-history.component.scss

.filter-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  margin-bottom: 24px;
  padding: 16px;
  background: var(--mat-sys-surface-container-low);
  border-radius: 12px;
}

.filter-field {
  min-width: 160px;

  &.date-range {
    min-width: 240px;
  }
}

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

// UXR-304: Touch targets at least 44x44px on mobile
@media (max-width: 599px) {
  .filter-bar {
    flex-direction: column;
    gap: 8px;

    .filter-field {
      width: 100%;
    }

    button {
      width: 100%;
      min-height: 44px;
    }
  }

  mat-paginator {
    ::ng-deep .mat-mdc-paginator-container {
      flex-wrap: wrap;
      justify-content: center;
    }

    ::ng-deep .mat-mdc-icon-button {
      min-width: 44px;
      min-height: 44px;
    }
  }
}

// UXR-303: Card layout below 768px (already from US_022 task_002)
// Existing styles handle table-to-card switch

// Tablet
@media (min-width: 600px) and (max-width: 1023px) {
  .filter-bar {
    flex-wrap: wrap;
  }

  .filter-field {
    flex: 1;
    min-width: 140px;
  }
}

// Desktop
@media (min-width: 1024px) {
  .filter-bar {
    flex-wrap: nowrap;
  }
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── features/
            │   ├── appointments/                      (existing from US_022)
            │   │   ├── appointment-history.component.ts    (modify)
            │   │   ├── appointment-history.component.html  (modify)
            │   │   ├── appointment-history.component.scss  (modify)
            │   │   ├── appointment-history-api.service.ts  (new)
            │   │   └── models/
            │   │       └── appointment-history.models.ts   (new)
            │   ├── booking/                            (existing)
            │   ├── scheduling/                         (existing)
            │   ├── intake/                             (existing)
            │   └── waitlist/                           (existing from US_023)
            └── app.routes.ts                           (no changes)
```

> Placeholder: Update on execution based on US_022 task_002 and US_024 task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/appointments/models/appointment-history.models.ts | TypeScript interfaces for filter, item, response, status enum |
| CREATE | client/src/app/features/appointments/appointment-history-api.service.ts | HttpClient service for paginated history and PDF export |
| MODIFY | client/src/app/features/appointments/appointment-history.component.ts | Server-side pagination, debounced filters, PDF export method |
| MODIFY | client/src/app/features/appointments/appointment-history.component.html | Status select, date range picker, export button, paginator, empty state |
| MODIFY | client/src/app/features/appointments/appointment-history.component.scss | Filter bar layout, touch targets, responsive breakpoints |

## External References

- Angular Material Paginator: https://material.angular.io/components/paginator/overview
- Angular Material Date Range Picker: https://material.angular.io/components/datepicker/overview#date-range-selection
- RxJS debounceTime + switchMap: https://rxjs.dev/api/operators/debounceTime

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Navigate to: http://localhost:4200/appointments/history
# Verify filter bar, paginator, empty state, and PDF export button
```

## Implementation Validation Strategy

- [ ] Appointment list displays sorted by date descending with status labels (AC-1)
- [ ] Status filter updates list within 500ms total (300ms debounce + server response) (AC-2)
- [ ] Date range picker filters appointments to selected range (AC-3)
- [ ] "Export PDF" button triggers blob download with correct filename (AC-4)
- [ ] Loading spinner on export button during PDF generation
- [ ] Paginator shows 20 items per page with correct total count (edge case)
- [ ] Empty state shows "No appointments found. Book your first appointment." (edge case)
- [ ] All filter controls and export button have keyboard navigation with focus indicators (UXR-202)
- [ ] Data table switches to card layout below 768px (UXR-303)
- [ ] Touch targets are at least 44x44px on mobile (UXR-304)
- [ ] Responsive layout at 375px, 768px, 1440px breakpoints (UXR-301)

## Implementation Checklist

- [x] Create TypeScript interfaces for filter, item, response, and status constants
- [x] Create `AppointmentHistoryApiService` with paginated GET and PDF export blob
- [x] Replace local data source with server-side pagination via `filterChange$` Subject
- [x] Add debounced filter pipeline (300ms debounce + switchMap) for status and date range
- [x] Add "Export PDF" button with loading spinner and blob download
- [x] Add mat-paginator connected to server page/totalCount
- [x] Add empty state message with booking CTA
- [x] Add 44x44px touch target sizing for mobile filter controls and paginator buttons
