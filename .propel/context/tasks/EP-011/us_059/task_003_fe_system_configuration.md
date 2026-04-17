# Task - TASK_003

## Requirement Reference

- User Story: us_059
- Story Location: .propel/context/tasks/EP-011/us_059/us_059.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I update a system configuration (e.g., reminder cadence, session timeout, slot duration template), Then the change is validated, saved with a version number and timestamp, and takes effect for new events from that point forward.
  - AC-2: Given I submit an invalid configuration value (e.g., a session timeout below the minimum), When the validation runs, Then the save is blocked and a descriptive error message explains the constraint.
  - AC-3: Given I want to review configuration history, When I open the configuration version history, Then all previous versions are listed with the change date, changed by (admin identity), and the before/after values.
  - AC-4: Given a configuration rollback is needed, When I select a previous version and click "Restore," Then the previous configuration is reapplied as a new version (not an overwrite) and takes effect immediately.
- Edge Cases:
  - What happens if two admins change the same configuration simultaneously? Optimistic concurrency control detects the conflict; the second admin is shown the current value and must confirm or cancel their change.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-019-system-config.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-019 |
| **UXR Requirements** | UXR-201, UXR-202, UXR-205, UXR-301, UXR-501 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-031 but SCR-031 does not exist in figma_spec.md. The correct screen for system configuration is **SCR-019** (System Configuration, EP-011, UC-006, Admin persona). SCR-019 specifies tabbed or accordion sections for each configuration category, current values with edit buttons, and 5 states (Default, Loading, Empty, Error, Validation). Layout: sidebar navigation for config categories on desktop, accordion on mobile.

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

Implement the SCR-019 System Configuration screen for admin users at the route `/admin/config`. The screen provides a `SystemConfigComponent` page container with a sidebar navigation on desktop (>= 768px) and an accordion layout on mobile (< 768px) per SCR-019 layout specification. Each configuration category (Slot Templates, Reminder Rules, Session Policy, Communication Templates) is a `ConfigCategoryComponent` that displays current values in a reactive form with inline validation errors (AC-2, UXR-205) and a Save button with loading spinner (UXR-501). On save, the component sends a `PUT /api/v1/admin/config/{category}` request with the current ETag in the `If-Match` header for optimistic concurrency (edge case 1). If a 409 Conflict is returned, a `ConflictDialogComponent` displays the current value and lets the admin confirm (overwrite with new ETag) or cancel. A `ConfigHistoryComponent` accessible via a "Version History" button per category section shows a `mat-table` with columns: version number, change date, changed by, and a before/after diff view (AC-3). Each history row has a "Restore" button that triggers `POST /api/v1/admin/config/{category}/restore/{versionId}` and creates a new version (AC-4) with a success toast confirmation. The screen implements all 5 SCR-019 states: Default (tabbed sections with current values), Loading (skeleton content per section), Empty (default values with "customize" prompts), Error (inline validation errors + save failure toast with retry), Validation (save confirmation toast, version history accessible). All components use Angular standalone architecture, signals, lazy-loaded route with adminGuard, and meet WCAG AA contrast (UXR-201), keyboard navigation (UXR-202), programmatic error association (UXR-205), responsive breakpoints (UXR-301), and loading spinners on submission (UXR-501).

## Dependent Tasks

- US_059 task_001 (requires configuration API endpoints: GET current, PUT update, GET history, POST restore)
- US_059 task_002 (requires configuration_versions table and seed data)
- US_015 task_001 (requires Admin route guard)

## Impacted Components

- New: `client/src/app/features/admin/config/system-config.component.ts` (page container with sidebar/accordion)
- New: `client/src/app/features/admin/config/system-config.component.html` (template)
- New: `client/src/app/features/admin/config/system-config.component.scss` (responsive styles)
- New: `client/src/app/features/admin/config/config-category.component.ts` (reactive form per category)
- New: `client/src/app/features/admin/config/config-category.component.html` (template)
- New: `client/src/app/features/admin/config/config-history.component.ts` (version history table)
- New: `client/src/app/features/admin/config/config-history.component.html` (template)
- New: `client/src/app/features/admin/config/conflict-dialog.component.ts` (OCC conflict resolution)
- New: `client/src/app/features/admin/config/conflict-dialog.component.html` (template)
- New: `client/src/app/features/admin/config/models/config.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/admin/config/config-api.service.ts` (HttpClient service)
- Modify: `client/src/app/app.routes.ts` (add admin/config route)

## Implementation Plan

1. **Create TypeScript interfaces** for configuration data:

```typescript
// client/src/app/features/admin/config/models/
//   config.models.ts

export type ConfigCategory =
  'SlotTemplates' | 'ReminderRules' |
  'SessionPolicy' | 'CommunicationTemplates';

export interface ConfigSnapshot {
  versionId: string;
  versionNumber: number;
  category: ConfigCategory;
  values: Record<string, unknown>;
  updatedAtUtc: string;
  updatedByName: string;
}

export interface ConfigVersion {
  id: string;
  versionNumber: number;
  updatedAtUtc: string;
  updatedByName: string;
  diff: {
    before: Record<string, unknown>;
    after: Record<string, unknown>;
  } | null;
  restoredFromVersionId: string | null;
}

export interface ConfigUpdateResult {
  versionId: string;
  versionNumber: number;
}

export const CONFIG_CATEGORIES: {
  key: ConfigCategory;
  label: string;
  icon: string;
}[] = [
  {
    key: 'SlotTemplates',
    label: 'Slot Templates',
    icon: 'schedule'
  },
  {
    key: 'ReminderRules',
    label: 'Reminder Rules',
    icon: 'notifications'
  },
  {
    key: 'SessionPolicy',
    label: 'Session Policy',
    icon: 'security'
  },
  {
    key: 'CommunicationTemplates',
    label: 'Communication Templates',
    icon: 'email'
  }
];
```

2. **Create `ConfigApiService`** with ETag handling:

```typescript
// client/src/app/features/admin/config/
//   config-api.service.ts
import { Injectable, inject } from '@angular/core';
import {
  HttpClient, HttpHeaders, HttpResponse
} from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ConfigCategory, ConfigSnapshot,
  ConfigVersion, ConfigUpdateResult
} from './models/config.models';

@Injectable({ providedIn: 'root' })
export class ConfigApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/config';

  getCurrent(
    category: ConfigCategory
  ): Observable<HttpResponse<ConfigSnapshot>> {
    return this.http
      .get<ConfigSnapshot>(
        `${this.base}/${category}`,
        { observe: 'response' });
  }

  update(
    category: ConfigCategory,
    values: Record<string, unknown>,
    etag: string
  ): Observable<ConfigUpdateResult> {
    return this.http
      .put<ConfigUpdateResult>(
        `${this.base}/${category}`,
        { values },
        {
          headers: new HttpHeaders({
            'If-Match': `"${etag}"`
          })
        });
  }

  getHistory(
    category: ConfigCategory
  ): Observable<ConfigVersion[]> {
    return this.http
      .get<ConfigVersion[]>(
        `${this.base}/${category}/history`);
  }

  restore(
    category: ConfigCategory, versionId: string
  ): Observable<ConfigUpdateResult> {
    return this.http
      .post<ConfigUpdateResult>(
        `${this.base}/${category}/restore/`
        + versionId, {});
  }
}
```

3. **Create `ConfigCategoryComponent`** with reactive form, inline validation, and ETag-based OCC:

```typescript
// client/src/app/features/admin/config/
//   config-category.component.ts
import {
  Component, input, signal, inject, OnInit
} from '@angular/core';
import {
  ReactiveFormsModule, FormGroup, FormControl
} from '@angular/forms';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatInputModule } from
  '@angular/material/input';
import { MatButtonModule } from
  '@angular/material/button';
import { MatProgressSpinnerModule } from
  '@angular/material/progress-spinner';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { MatDialog } from
  '@angular/material/dialog';
import { HttpErrorResponse } from
  '@angular/common/http';
import { ConfigApiService } from
  './config-api.service';
import { ConfigCategory, ConfigSnapshot } from
  './models/config.models';
import { ConflictDialogComponent } from
  './conflict-dialog.component';

@Component({
  selector: 'app-config-category',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatFormFieldModule,
    MatInputModule, MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl:
    './config-category.component.html'
})
export class ConfigCategoryComponent
    implements OnInit {
  readonly category = input.required<ConfigCategory>();
  private readonly api = inject(ConfigApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly form = signal<FormGroup | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly etag = signal<string>('');
  readonly snapshot =
    signal<ConfigSnapshot | null>(null);

  ngOnInit(): void {
    this.loadConfig();
  }

  loadConfig(): void {
    this.loading.set(true);
    this.api.getCurrent(this.category())
      .subscribe({
        next: (response) => {
          const config = response.body!;
          this.snapshot.set(config);
          this.etag.set(
            response.headers.get('ETag')
              ?.replace(/"/g, '') ?? '');
          this.buildForm(config.values);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      });
  }

  save(): void {
    if (!this.form()?.valid) return;
    this.saving.set(true);

    this.api.update(
      this.category(),
      this.form()!.value,
      this.etag()
    ).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.etag.set(
          String(result.versionNumber));
        this.snackBar.open(
          'Configuration saved (v'
          + result.versionNumber + ')',
          'Dismiss', { duration: 3000 });
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        if (err.status === 409) {
          this.handleConflict(err.error);
        } else if (err.status === 422) {
          this.showValidationErrors(err.error);
        } else {
          this.snackBar.open(
            'Save failed. Try again.',
            'Retry', { duration: 5000 });
        }
      }
    });
  }

  private handleConflict(
    currentValue: ConfigSnapshot
  ): void {
    const dialogRef = this.dialog.open(
      ConflictDialogComponent, {
        data: {
          yourValues: this.form()!.value,
          currentValues: currentValue.values,
          updatedBy: currentValue.updatedByName
        },
        width: '500px'
      });

    dialogRef.afterClosed().subscribe(
      (confirmed: boolean) => {
        if (confirmed) {
          this.etag.set(
            String(currentValue.versionNumber));
          this.save();
        } else {
          this.buildForm(currentValue.values);
          this.etag.set(
            String(currentValue.versionNumber));
        }
      });
  }

  private showValidationErrors(
    errors: string[]
  ): void {
    this.snackBar.open(
      errors.join('; '), 'Dismiss',
      { duration: 5000 });
  }

  private buildForm(
    values: Record<string, unknown>
  ): void {
    const controls: Record<string, FormControl> = {};
    for (const [key, val] of Object.entries(values)) {
      controls[key] = new FormControl(val);
    }
    this.form.set(new FormGroup(controls));
  }
}
```

```html
<!-- config-category.component.html -->
@if (loading()) {
  <div class="skeleton-form">
    <div class="skeleton-line"></div>
    <div class="skeleton-line"></div>
    <div class="skeleton-line"></div>
  </div>
} @else if (form(); as f) {
  <form [formGroup]="f" (ngSubmit)="save()">
    @for (control of f.controls | keyvalue;
          track control.key) {
      <mat-form-field appearance="outline"
                      class="full-width">
        <mat-label>{{ control.key }}</mat-label>
        <input matInput
               [formControlName]="control.key"
               [attr.aria-describedby]="
                 control.key + '-error'">
        @if (f.get(control.key)?.errors) {
          <mat-error
            [id]="control.key + '-error'">
            {{ f.get(control.key)
                ?.errors?.['serverError']
              || 'Invalid value' }}
          </mat-error>
        }
      </mat-form-field>
    }

    <div class="form-actions">
      <button mat-raised-button
              color="primary"
              type="submit"
              [disabled]="saving() || !f.valid">
        @if (saving()) {
          <mat-spinner diameter="20"
                       class="btn-spinner">
          </mat-spinner>
          Saving...
        } @else {
          Save Configuration
        }
      </button>

      <button mat-button
              type="button"
              (click)="loadConfig()">
        Reset
      </button>
    </div>
  </form>

  @if (snapshot()) {
    <p class="version-info">
      Version {{ snapshot()!.versionNumber }} —
      Last updated
      {{ snapshot()!.updatedAtUtc | date:'short' }}
      by {{ snapshot()!.updatedByName }}
    </p>
  }
}
```

4. **Create `ConfigHistoryComponent`** for version history with diff view and restore (AC-3, AC-4):

```typescript
// client/src/app/features/admin/config/
//   config-history.component.ts
import {
  Component, input, signal, inject, OnInit
} from '@angular/core';
import { MatTableModule } from
  '@angular/material/table';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { DatePipe, JsonPipe } from '@angular/common';
import { ConfigApiService } from
  './config-api.service';
import { ConfigCategory, ConfigVersion } from
  './models/config.models';

@Component({
  selector: 'app-config-history',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule,
    MatIconModule, DatePipe, JsonPipe
  ],
  templateUrl:
    './config-history.component.html'
})
export class ConfigHistoryComponent
    implements OnInit {
  readonly category =
    input.required<ConfigCategory>();
  readonly onRestored = signal(false);

  private readonly api = inject(ConfigApiService);
  private readonly snackBar = inject(MatSnackBar);

  readonly versions = signal<ConfigVersion[]>([]);
  readonly loading = signal(false);
  readonly expandedRow =
    signal<string | null>(null);

  readonly displayedColumns = [
    'version', 'updatedAt', 'updatedBy',
    'restored', 'actions'
  ];

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loading.set(true);
    this.api.getHistory(this.category()).subscribe({
      next: (data) => {
        this.versions.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  toggleDiff(versionId: string): void {
    this.expandedRow.set(
      this.expandedRow() === versionId
        ? null : versionId);
  }

  restore(version: ConfigVersion): void {
    this.api.restore(
      this.category(), version.id
    ).subscribe({
      next: (result) => {
        this.snackBar.open(
          'Restored as version '
          + result.versionNumber,
          'Dismiss', { duration: 3000 });
        this.loadHistory();
        this.onRestored.set(true);
      },
      error: () =>
        this.snackBar.open(
          'Restore failed', 'Dismiss',
          { duration: 5000 })
    });
  }
}
```

```html
<!-- config-history.component.html -->
<h3>Version History</h3>

@if (loading()) {
  <p>Loading history...</p>
} @else if (versions().length === 0) {
  <p>No version history available.</p>
} @else {
  <table mat-table
         [dataSource]="versions()"
         aria-label="Configuration version history">

    <ng-container matColumnDef="version">
      <th mat-header-cell *matHeaderCellDef>
        Version
      </th>
      <td mat-cell *matCellDef="let v">
        v{{ v.versionNumber }}
      </td>
    </ng-container>

    <ng-container matColumnDef="updatedAt">
      <th mat-header-cell *matHeaderCellDef>
        Changed
      </th>
      <td mat-cell *matCellDef="let v">
        {{ v.updatedAtUtc | date:'short' }}
      </td>
    </ng-container>

    <ng-container matColumnDef="updatedBy">
      <th mat-header-cell *matHeaderCellDef>
        Changed By
      </th>
      <td mat-cell *matCellDef="let v">
        {{ v.updatedByName }}
      </td>
    </ng-container>

    <ng-container matColumnDef="restored">
      <th mat-header-cell *matHeaderCellDef>
        Restored
      </th>
      <td mat-cell *matCellDef="let v">
        @if (v.restoredFromVersionId) {
          <mat-icon>restore</mat-icon>
        }
      </td>
    </ng-container>

    <ng-container matColumnDef="actions">
      <th mat-header-cell *matHeaderCellDef>
        Actions
      </th>
      <td mat-cell *matCellDef="let v">
        <button mat-icon-button
                (click)="toggleDiff(v.id)"
                aria-label="View changes">
          <mat-icon>compare_arrows</mat-icon>
        </button>
        <button mat-icon-button
                (click)="restore(v)"
                aria-label="Restore this version">
          <mat-icon>restore</mat-icon>
        </button>
      </td>
    </ng-container>

    <tr mat-header-row
        *matHeaderRowDef="displayedColumns">
    </tr>
    <tr mat-row
        *matRowDef="let row;
                    columns: displayedColumns"
        [class.expanded]="
          expandedRow() === row.id">
    </tr>
  </table>

  <!-- Expanded diff row -->
  @if (expandedRow(); as rowId) {
    @for (v of versions(); track v.id) {
      @if (v.id === rowId && v.diff) {
        <div class="diff-panel">
          <div class="diff-before">
            <strong>Before:</strong>
            <pre>{{ v.diff.before | json }}</pre>
          </div>
          <div class="diff-after">
            <strong>After:</strong>
            <pre>{{ v.diff.after | json }}</pre>
          </div>
        </div>
      }
    }
  }
}
```

5. **Create `ConflictDialogComponent`** for optimistic concurrency conflict resolution (edge case 1):

```typescript
// client/src/app/features/admin/config/
//   conflict-dialog.component.ts
import { Component, inject } from '@angular/core';
import {
  MAT_DIALOG_DATA, MatDialogModule,
  MatDialogRef
} from '@angular/material/dialog';
import { MatButtonModule } from
  '@angular/material/button';
import { JsonPipe } from '@angular/common';

@Component({
  selector: 'app-conflict-dialog',
  standalone: true,
  imports: [
    MatDialogModule, MatButtonModule, JsonPipe
  ],
  template: `
    <h2 mat-dialog-title>
      Configuration Conflict Detected
    </h2>
    <mat-dialog-content>
      <p>
        Another admin ({{ data.updatedBy }})
        has updated this configuration.
      </p>
      <div class="conflict-values">
        <div>
          <strong>Your changes:</strong>
          <pre>{{ data.yourValues | json }}</pre>
        </div>
        <div>
          <strong>Current server value:</strong>
          <pre>{{ data.currentValues | json }}</pre>
        </div>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button
              (click)="dialogRef.close(false)">
        Cancel (Use Server Value)
      </button>
      <button mat-raised-button
              color="primary"
              (click)="dialogRef.close(true)">
        Overwrite With My Changes
      </button>
    </mat-dialog-actions>
  `
})
export class ConflictDialogComponent {
  readonly data = inject(MAT_DIALOG_DATA);
  readonly dialogRef =
    inject(MatDialogRef<ConflictDialogComponent>);
}
```

6. **Create `SystemConfigComponent`** as the page container with sidebar navigation (desktop) and accordion (mobile) per SCR-019:

```typescript
// client/src/app/features/admin/config/
//   system-config.component.ts
import { Component, signal } from '@angular/core';
import { MatSidenavModule } from
  '@angular/material/sidenav';
import { MatListModule } from
  '@angular/material/list';
import { MatExpansionModule } from
  '@angular/material/expansion';
import { MatIconModule } from
  '@angular/material/icon';
import { MatButtonModule } from
  '@angular/material/button';
import { ConfigCategoryComponent } from
  './config-category.component';
import { ConfigHistoryComponent } from
  './config-history.component';
import {
  ConfigCategory, CONFIG_CATEGORIES
} from './models/config.models';

@Component({
  selector: 'app-system-config',
  standalone: true,
  imports: [
    MatSidenavModule, MatListModule,
    MatExpansionModule, MatIconModule,
    MatButtonModule,
    ConfigCategoryComponent,
    ConfigHistoryComponent
  ],
  templateUrl:
    './system-config.component.html',
  styleUrl:
    './system-config.component.scss'
})
export class SystemConfigComponent {
  readonly categories = CONFIG_CATEGORIES;
  readonly selectedCategory =
    signal<ConfigCategory>('SlotTemplates');
  readonly showHistory = signal(false);
  readonly historyCategory =
    signal<ConfigCategory | null>(null);

  selectCategory(category: ConfigCategory): void {
    this.selectedCategory.set(category);
    this.showHistory.set(false);
  }

  toggleHistory(category: ConfigCategory): void {
    if (this.historyCategory() === category
        && this.showHistory()) {
      this.showHistory.set(false);
    } else {
      this.historyCategory.set(category);
      this.showHistory.set(true);
    }
  }
}
```

```html
<!-- system-config.component.html -->
<div class="config-container">
  <h1>System Configuration</h1>

  <!-- Desktop layout (>= 768px): sidebar nav -->
  <div class="desktop-layout">
    <mat-sidenav-container>
      <mat-sidenav mode="side" opened
                   class="config-sidebar">
        <mat-nav-list>
          @for (cat of categories; track cat.key) {
            <a mat-list-item
               [activated]="
                 selectedCategory() === cat.key"
               (click)="selectCategory(cat.key)">
              <mat-icon matListItemIcon>
                {{ cat.icon }}
              </mat-icon>
              {{ cat.label }}
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>

      <mat-sidenav-content class="config-content">
        <app-config-category
          [category]="selectedCategory()">
        </app-config-category>

        <button mat-button
                color="accent"
                (click)="toggleHistory(
                  selectedCategory())">
          @if (showHistory()
            && historyCategory()
              === selectedCategory()) {
            Hide Version History
          } @else {
            View Version History
          }
        </button>

        @if (showHistory()
          && historyCategory()
            === selectedCategory()) {
          <app-config-history
            [category]="selectedCategory()">
          </app-config-history>
        }
      </mat-sidenav-content>
    </mat-sidenav-container>
  </div>

  <!-- Mobile layout (< 768px): accordion -->
  <div class="mobile-layout">
    <mat-accordion>
      @for (cat of categories; track cat.key) {
        <mat-expansion-panel>
          <mat-expansion-panel-header>
            <mat-panel-title>
              <mat-icon>{{ cat.icon }}</mat-icon>
              {{ cat.label }}
            </mat-panel-title>
          </mat-expansion-panel-header>

          <app-config-category
            [category]="cat.key">
          </app-config-category>

          <button mat-button
                  color="accent"
                  (click)="toggleHistory(cat.key)">
            Version History
          </button>

          @if (showHistory()
            && historyCategory() === cat.key) {
            <app-config-history
              [category]="cat.key">
            </app-config-history>
          }
        </mat-expansion-panel>
      }
    </mat-accordion>
  </div>
</div>
```

7. **Add lazy-loaded route** with admin guard:

```typescript
// In app.routes.ts
{
  path: 'admin/config',
  loadComponent: () =>
    import(
      './features/admin/config/' +
      'system-config.component'
    ).then(m => m.SystemConfigComponent),
  canActivate: [adminGuard]
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
                └── admin/
                    └── config/
                        ├── system-config.component.ts          (new)
                        ├── system-config.component.html        (new)
                        ├── system-config.component.scss        (new)
                        ├── config-category.component.ts        (new)
                        ├── config-category.component.html      (new)
                        ├── config-history.component.ts         (new)
                        ├── config-history.component.html       (new)
                        ├── conflict-dialog.component.ts        (new)
                        ├── models/
                        │   └── config.models.ts                (new)
                        └── config-api.service.ts               (new)
```

> Placeholder: Update on execution based on US_059 task_001 and task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/admin/config/models/config.models.ts | TypeScript interfaces for snapshots, versions, categories, update results |
| CREATE | client/src/app/features/admin/config/config-api.service.ts | HttpClient with ETag handling for GET, PUT, history, restore |
| CREATE | client/src/app/features/admin/config/config-category.component.ts | Reactive form per category with inline validation and OCC |
| CREATE | client/src/app/features/admin/config/config-category.component.html | Form template with mat-form-fields, error messages, save button |
| CREATE | client/src/app/features/admin/config/config-history.component.ts | Version history table with diff expansion and restore button |
| CREATE | client/src/app/features/admin/config/config-history.component.html | History table with before/after diff panel |
| CREATE | client/src/app/features/admin/config/conflict-dialog.component.ts | OCC conflict resolution dialog with confirm/cancel |
| CREATE | client/src/app/features/admin/config/system-config.component.ts | Page container with sidebar (desktop) and accordion (mobile) |
| CREATE | client/src/app/features/admin/config/system-config.component.html | Responsive layout per SCR-019 specification |
| CREATE | client/src/app/features/admin/config/system-config.component.scss | Sidebar/accordion responsive styles |
| MODIFY | client/src/app/app.routes.ts | Add /admin/config route with adminGuard |

## External References

- Angular Material Sidenav: https://material.angular.io/components/sidenav/overview
- Angular Material Expansion Panel: https://material.angular.io/components/expansion/overview
- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- Angular Reactive Forms: https://angular.dev/guide/forms/reactive-forms
- Angular Signals: https://angular.dev/guide/signals
- WCAG 2.1 AA Error Identification: https://www.w3.org/WAI/WCAG21/Understanding/error-identification.html
- ARIA describedby for Error Association: https://www.w3.org/WAI/WCAG21/Techniques/aria/ARIA21

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Test configuration flow:
# 1. Log in as Admin
# 2. Navigate to /admin/config
# 3. Select Session Policy from sidebar
# 4. Change timeout to 3 (below min) → verify error
# 5. Change timeout to 20 → Save → verify success
# 6. Click Version History → verify entries
# 7. Click Restore on a previous version → verify
# 8. Open second browser as another admin →
#    trigger concurrent edit → verify conflict dialog
```

## Implementation Validation Strategy

- [ ] Configuration form renders current values for all 4 categories (AC-1)
- [ ] Invalid values show inline errors with descriptive messages (AC-2, UXR-205)
- [ ] Version history displays version, date, admin, before/after diff (AC-3)
- [ ] Restore creates new version and refreshes form (AC-4)
- [ ] Concurrent edit shows conflict dialog with current server value (edge case 1)
- [ ] Save button shows loading spinner during submission (UXR-501)
- [ ] Sidebar layout on desktop, accordion on mobile per SCR-019
- [ ] Text meets WCAG AA 4.5:1 contrast ratio (UXR-201)
- [ ] All interactive elements keyboard navigable (UXR-202)
- [ ] Responsive layout at 375px, 768px, 1440px breakpoints (UXR-301)

## Implementation Checklist

- [ ] Create TypeScript interfaces for config snapshots, versions, categories, and update results
- [ ] Implement ConfigApiService with ETag handling for GET, PUT, history, and restore operations
- [ ] Build ConfigCategoryComponent with reactive form, inline validation, and save with spinner
- [ ] Build ConfigHistoryComponent with version table, expandable diff view, and restore button
- [ ] Build ConflictDialogComponent for OCC conflict resolution with confirm/cancel actions
- [ ] Build SystemConfigComponent with sidebar navigation (desktop) and accordion (mobile)
- [ ] Implement all 5 SCR-019 states (Default, Loading, Empty, Error, Validation)
- [ ] Add lazy-loaded route with adminGuard and register in app.routes.ts
