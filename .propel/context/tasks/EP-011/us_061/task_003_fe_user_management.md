# Task - TASK_003

## Requirement Reference

- User Story: us_061
- Story Location: .propel/context/tasks/EP-011/us_061/us_061.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I navigate to user management, Then all users are listed with name, email, role, status, and last active date with pagination and search by name/email.
  - AC-2: Given I select multiple users using checkboxes, When I apply a bulk action (Activate, Deactivate, or Assign Role), Then the action is applied to all selected users in a single operation and each change is recorded in the audit log.
  - AC-3: Given I view a specific user's profile, When I open their activity history, Then recent login events, role changes, and actions performed are listed in reverse chronological order.
  - AC-4: Given I bulk deactivate 50 users, When the operation completes, Then a summary confirmation shows "50 users deactivated" and lists any users where the action failed (e.g., attempting to deactivate the last admin).
- Edge Cases:
  - What happens if a bulk action would deactivate all admin accounts? System validates the action and blocks it with: "Cannot deactivate all admin accounts. At least one admin must remain active."
  - How does the system handle role assignment to a user type that doesn't support the role? Role assignment is validated against allowed role-user-type mappings; invalid assignments return a descriptive error.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-020-user-management.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-020 |
| **UXR Requirements** | UXR-201, UXR-202, UXR-301, UXR-303, UXR-304, UXR-501 |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-033 but SCR-033 does not exist in figma_spec.md. The correct screen for user management is **SCR-020** (User Management, EP-011, UC-006, Admin persona). SCR-020 specifies a full-width data table with toolbar for search, filter, and bulk actions. User detail side panel on row click. Bulk action toolbar with checkbox selection. Invite button. 5 states (Default, Loading, Empty, Error, Validation). Confirmation dialog for deactivation and bulk actions.

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

Implement the SCR-020 User Management screen for admin users at the route `/admin/users`. The screen renders a `UserManagementComponent` page container with a toolbar containing a search input (name/email), role filter dropdown, status filter dropdown, and an "Invite User" button. Below the toolbar, a `mat-table` displays columns: checkbox (for bulk selection), name, email, role, status badge, and last active date with server-side pagination (AC-1). A `SelectionModel` tracks selected rows; when one or more rows are selected, a bulk action toolbar appears with Activate, Deactivate, and Assign Role buttons. Clicking a bulk action opens a `BulkActionConfirmDialogComponent` to confirm the operation (AC-2). After completion, a `BulkActionResultDialogComponent` shows the summary: "{N} users {action}" with a list of any failures and reasons (AC-4). Clicking a user row opens a `UserDetailPanelComponent` side panel showing user profile details and an `ActivityHistoryComponent` that lists login events, role changes, and actions in reverse chronological order with pagination (AC-3). The bulk deactivation path includes a special error state when the backend returns a last-admin guard error (edge case 1). Role assignment via bulk action validates against the backend; invalid role-type errors are displayed per-user in the result dialog (edge case 2). The data table switches to a card-based layout on screens below 768px per UXR-303. Touch targets are at least 44x44px on mobile per UXR-304. The screen implements all 5 SCR-020 states: Default (user table with bulk toolbar), Loading (skeleton table rows), Empty ("No users found" with invite CTA), Error (retry banner on load failure, error toast for failed bulk action), Validation (confirmation dialog for deactivation/bulk, success toast for invite sent). All components use Angular standalone architecture, signals, lazy-loaded route with adminGuard, and meet WCAG AA contrast (UXR-201), keyboard navigation (UXR-202), responsive breakpoints (UXR-301), and loading spinner on submit (UXR-501).

## Dependent Tasks

- US_061 task_001 (requires user management API endpoints: list, get, bulk action, activity history)
- US_061 task_002 (requires user_activity_log table)
- US_015 task_001 (requires Admin route guard)

## Impacted Components

- New: `client/src/app/features/admin/users/user-management.component.ts` (page container)
- New: `client/src/app/features/admin/users/user-management.component.html` (template)
- New: `client/src/app/features/admin/users/user-management.component.scss` (responsive styles)
- New: `client/src/app/features/admin/users/user-detail-panel.component.ts` (side panel)
- New: `client/src/app/features/admin/users/user-detail-panel.component.html` (template)
- New: `client/src/app/features/admin/users/activity-history.component.ts` (activity log list)
- New: `client/src/app/features/admin/users/activity-history.component.html` (template)
- New: `client/src/app/features/admin/users/bulk-action-confirm-dialog.component.ts` (confirm dialog)
- New: `client/src/app/features/admin/users/bulk-action-result-dialog.component.ts` (result summary)
- New: `client/src/app/features/admin/users/models/user.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/admin/users/user-api.service.ts` (HttpClient service)
- Modify: `client/src/app/app.routes.ts` (add admin/users route)

## Implementation Plan

1. **Create TypeScript interfaces** for user management data:

```typescript
// client/src/app/features/admin/users/models/
//   user.models.ts

export interface UserListItem {
  userId: string;
  name: string;
  email: string;
  role: string;
  status: string;
  lastActiveUtc: string | null;
}

export interface UserDetail {
  userId: string;
  name: string;
  email: string;
  role: string;
  status: string;
  lastActiveUtc: string | null;
  createdAtUtc: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type BulkActionType =
  'Activate' | 'Deactivate' | 'AssignRole';

export interface BulkActionRequest {
  userIds: string[];
  action: BulkActionType;
  targetRole?: string;
}

export interface BulkActionFailure {
  userId: string;
  userName: string;
  reason: string;
}

export interface BulkActionResult {
  successCount: number;
  failureCount: number;
  failures: BulkActionFailure[];
}

export interface UserActivityEntry {
  id: string;
  eventType: string;
  description: string;
  occurredAtUtc: string;
  performedByName: string | null;
}
```

2. **Create `UserApiService`** with HttpClient:

```typescript
// client/src/app/features/admin/users/
//   user-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from
  '@angular/common/http';
import { Observable } from 'rxjs';
import {
  UserListItem, UserDetail,
  PagedResult, BulkActionRequest,
  BulkActionResult, UserActivityEntry
} from './models/user.models';

@Injectable({ providedIn: 'root' })
export class UserApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/users';

  list(
    searchTerm?: string,
    roleFilter?: string,
    statusFilter?: string,
    page = 1,
    pageSize = 25
  ): Observable<PagedResult<UserListItem>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (searchTerm)
      params = params.set(
        'searchTerm', searchTerm);
    if (roleFilter)
      params = params.set(
        'roleFilter', roleFilter);
    if (statusFilter)
      params = params.set(
        'statusFilter', statusFilter);
    return this.http
      .get<PagedResult<UserListItem>>(
        this.base, { params });
  }

  getById(
    userId: string
  ): Observable<UserDetail> {
    return this.http.get<UserDetail>(
      `${this.base}/${userId}`);
  }

  bulkAction(
    request: BulkActionRequest
  ): Observable<BulkActionResult> {
    return this.http
      .post<BulkActionResult>(
        `${this.base}/bulk`, request);
  }

  getActivityHistory(
    userId: string, page = 1, pageSize = 25
  ): Observable<UserActivityEntry[]> {
    return this.http
      .get<UserActivityEntry[]>(
        `${this.base}/${userId}/activity`,
        { params: { page, pageSize } });
  }
}
```

3. **Create `UserManagementComponent`** with data table, search, filters, and bulk selection:

```typescript
// client/src/app/features/admin/users/
//   user-management.component.ts
import {
  Component, signal, inject,
  OnInit, ViewChild
} from '@angular/core';
import { SelectionModel } from
  '@angular/cdk/collections';
import { MatTableModule } from
  '@angular/material/table';
import { MatCheckboxModule } from
  '@angular/material/checkbox';
import { MatPaginatorModule, MatPaginator,
  PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { MatInputModule } from
  '@angular/material/input';
import { MatSelectModule } from
  '@angular/material/select';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import { MatChipsModule } from
  '@angular/material/chips';
import { MatSnackBar } from
  '@angular/material/snack-bar';
import { MatDialog } from
  '@angular/material/dialog';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UserApiService } from
  './user-api.service';
import { UserDetailPanelComponent } from
  './user-detail-panel.component';
import {
  BulkActionConfirmDialogComponent
} from './bulk-action-confirm-dialog.component';
import {
  BulkActionResultDialogComponent
} from './bulk-action-result-dialog.component';
import {
  UserListItem, BulkActionType
} from './models/user.models';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [
    MatTableModule, MatCheckboxModule,
    MatPaginatorModule, MatFormFieldModule,
    MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule,
    MatChipsModule, FormsModule, DatePipe,
    UserDetailPanelComponent
  ],
  templateUrl:
    './user-management.component.html',
  styleUrl:
    './user-management.component.scss'
})
export class UserManagementComponent
    implements OnInit {
  private readonly api = inject(UserApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  @ViewChild(MatPaginator)
  paginator!: MatPaginator;

  readonly users = signal<UserListItem[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly searchTerm = signal('');
  readonly roleFilter = signal('');
  readonly statusFilter = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly selectedUser =
    signal<UserListItem | null>(null);

  readonly selection =
    new SelectionModel<UserListItem>(
      true, []);

  readonly displayedColumns = [
    'select', 'name', 'email', 'role',
    'status', 'lastActive'
  ];

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.loading.set(true);
    this.api.list(
      this.searchTerm(),
      this.roleFilter(),
      this.statusFilter(),
      this.page(),
      this.pageSize()
    ).subscribe({
      next: (result) => {
        this.users.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
        this.selection.clear();
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open(
          'Failed to load users', 'Retry',
          { duration: 5000 });
      }
    });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.page.set(1);
    this.loadUsers();
  }

  onFilterChange(): void {
    this.page.set(1);
    this.loadUsers();
  }

  onPageChange(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.loadUsers();
  }

  onRowClick(user: UserListItem): void {
    this.selectedUser.set(user);
  }

  isAllSelected(): boolean {
    return this.selection.selected.length ===
      this.users().length;
  }

  toggleAllRows(): void {
    if (this.isAllSelected()) {
      this.selection.clear();
    } else {
      this.selection.select(
        ...this.users());
    }
  }

  executeBulkAction(
    action: BulkActionType
  ): void {
    const dialogRef = this.dialog.open(
      BulkActionConfirmDialogComponent, {
        data: {
          action,
          count: this.selection.selected.length
        },
        width: '400px'
      });

    dialogRef.afterClosed().subscribe(
      (confirmed) => {
        if (!confirmed) return;

        this.api.bulkAction({
          userIds: this.selection.selected
            .map(u => u.userId),
          action,
          targetRole: confirmed.targetRole
        }).subscribe({
          next: (result) => {
            this.dialog.open(
              BulkActionResultDialogComponent, {
                data: { result, action },
                width: '500px'
              });
            this.loadUsers();
          },
          error: (err) => {
            this.snackBar.open(
              err.error?.message
                ?? 'Bulk action failed',
              'Dismiss', { duration: 5000 });
          }
        });
      });
  }

  closePanel(): void {
    this.selectedUser.set(null);
  }
}
```

```html
<!-- user-management.component.html -->
<div class="user-management">
  <header class="page-header">
    <h1>User Management</h1>
    <button mat-raised-button color="primary">
      <mat-icon>person_add</mat-icon>
      Invite User
    </button>
  </header>

  <!-- Search and Filters Toolbar -->
  <div class="toolbar">
    <mat-form-field appearance="outline"
                    class="search-field">
      <mat-label>Search by name or email</mat-label>
      <input matInput
             [ngModel]="searchTerm()"
             (ngModelChange)="onSearch($event)"
             aria-label="Search users">
      <mat-icon matSuffix>search</mat-icon>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Role</mat-label>
      <mat-select [ngModel]="roleFilter()"
                  (ngModelChange)="
                    roleFilter.set($event);
                    onFilterChange()">
        <mat-option value="">All Roles</mat-option>
        <mat-option value="Admin">Admin</mat-option>
        <mat-option value="Staff">Staff</mat-option>
        <mat-option value="Clinician">
          Clinician
        </mat-option>
        <mat-option value="Patient">
          Patient
        </mat-option>
      </mat-select>
    </mat-form-field>

    <mat-form-field appearance="outline">
      <mat-label>Status</mat-label>
      <mat-select [ngModel]="statusFilter()"
                  (ngModelChange)="
                    statusFilter.set($event);
                    onFilterChange()">
        <mat-option value="">All Statuses</mat-option>
        <mat-option value="Active">Active</mat-option>
        <mat-option value="Inactive">
          Inactive
        </mat-option>
      </mat-select>
    </mat-form-field>
  </div>

  <!-- Bulk Action Toolbar -->
  @if (selection.selected.length > 0) {
    <div class="bulk-toolbar"
         role="toolbar"
         aria-label="Bulk actions">
      <span>
        {{ selection.selected.length }} selected
      </span>
      <button mat-button
              (click)="executeBulkAction(
                'Activate')">
        <mat-icon>check_circle</mat-icon>
        Activate
      </button>
      <button mat-button
              color="warn"
              (click)="executeBulkAction(
                'Deactivate')">
        <mat-icon>block</mat-icon>
        Deactivate
      </button>
      <button mat-button
              (click)="executeBulkAction(
                'AssignRole')">
        <mat-icon>assignment_ind</mat-icon>
        Assign Role
      </button>
    </div>
  }

  <!-- Loading State -->
  @if (loading()) {
    <div class="skeleton-table">
      @for (i of [1,2,3,4,5]; track i) {
        <div class="skeleton-row"></div>
      }
    </div>
  } @else if (users().length === 0) {
    <!-- Empty State -->
    <div class="empty-state">
      <mat-icon>group_off</mat-icon>
      <h2>No users found</h2>
      <p>Invite users to get started.</p>
      <button mat-raised-button color="primary">
        <mat-icon>person_add</mat-icon>
        Invite User
      </button>
    </div>
  } @else {
    <!-- Desktop: Data Table -->
    <div class="desktop-table">
      <table mat-table
             [dataSource]="users()"
             aria-label="User list">

        <!-- Checkbox Column -->
        <ng-container matColumnDef="select">
          <th mat-header-cell *matHeaderCellDef>
            <mat-checkbox
              (change)="toggleAllRows()"
              [checked]="isAllSelected()"
              [indeterminate]="
                selection.hasValue()
                && !isAllSelected()"
              aria-label="Select all users">
            </mat-checkbox>
          </th>
          <td mat-cell *matCellDef="let row">
            <mat-checkbox
              (click)="$event.stopPropagation()"
              (change)="selection.toggle(row)"
              [checked]="selection.isSelected(row)"
              [attr.aria-label]="
                'Select ' + row.name">
            </mat-checkbox>
          </td>
        </ng-container>

        <!-- Name Column -->
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>
            Name
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.name }}
          </td>
        </ng-container>

        <!-- Email Column -->
        <ng-container matColumnDef="email">
          <th mat-header-cell *matHeaderCellDef>
            Email
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.email }}
          </td>
        </ng-container>

        <!-- Role Column -->
        <ng-container matColumnDef="role">
          <th mat-header-cell *matHeaderCellDef>
            Role
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.role }}
          </td>
        </ng-container>

        <!-- Status Column -->
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>
            Status
          </th>
          <td mat-cell *matCellDef="let row">
            <span class="status-badge"
                  [class.active]="
                    row.status === 'Active'"
                  [class.inactive]="
                    row.status === 'Inactive'">
              {{ row.status }}
            </span>
          </td>
        </ng-container>

        <!-- Last Active Column -->
        <ng-container matColumnDef="lastActive">
          <th mat-header-cell *matHeaderCellDef>
            Last Active
          </th>
          <td mat-cell *matCellDef="let row">
            {{ row.lastActiveUtc
                | date:'short' ?? 'Never' }}
          </td>
        </ng-container>

        <tr mat-header-row
            *matHeaderRowDef="displayedColumns">
        </tr>
        <tr mat-row
            *matRowDef="let row;
                        columns: displayedColumns"
            (click)="onRowClick(row)"
            [class.selected]="
              selectedUser()?.userId
                === row.userId"
            class="clickable-row">
        </tr>
      </table>

      <mat-paginator
        [length]="totalCount()"
        [pageSize]="pageSize()"
        [pageSizeOptions]="[10, 25, 50]"
        (page)="onPageChange($event)"
        aria-label="User list pagination">
      </mat-paginator>
    </div>

    <!-- Mobile: Card Layout (< 768px) -->
    <div class="mobile-cards">
      @for (user of users(); track user.userId) {
        <div class="user-card"
             (click)="onRowClick(user)"
             role="button"
             tabindex="0"
             [attr.aria-label]="
               user.name + ', ' + user.role">
          <mat-checkbox
            (click)="$event.stopPropagation()"
            (change)="selection.toggle(user)"
            [checked]="
              selection.isSelected(user)"
            class="card-checkbox">
          </mat-checkbox>
          <div class="card-content">
            <strong>{{ user.name }}</strong>
            <span>{{ user.email }}</span>
            <div class="card-meta">
              <span>{{ user.role }}</span>
              <span class="status-badge"
                    [class.active]="
                      user.status === 'Active'"
                    [class.inactive]="
                      user.status === 'Inactive'">
                {{ user.status }}
              </span>
            </div>
          </div>
        </div>
      }
    </div>
  }

  <!-- User Detail Side Panel -->
  @if (selectedUser(); as user) {
    <app-user-detail-panel
      [userId]="user.userId"
      (closed)="closePanel()">
    </app-user-detail-panel>
  }
</div>
```

4. **Create `UserDetailPanelComponent`** with profile and activity history (AC-3):

```typescript
// client/src/app/features/admin/users/
//   user-detail-panel.component.ts
import {
  Component, input, output, signal,
  inject, OnInit
} from '@angular/core';
import { MatIconModule } from
  '@angular/material/icon';
import { MatButtonModule } from
  '@angular/material/button';
import { MatDividerModule } from
  '@angular/material/divider';
import { DatePipe } from '@angular/common';
import { UserApiService } from
  './user-api.service';
import { ActivityHistoryComponent } from
  './activity-history.component';
import { UserDetail } from
  './models/user.models';

@Component({
  selector: 'app-user-detail-panel',
  standalone: true,
  imports: [
    MatIconModule, MatButtonModule,
    MatDividerModule, DatePipe,
    ActivityHistoryComponent
  ],
  templateUrl:
    './user-detail-panel.component.html'
})
export class UserDetailPanelComponent
    implements OnInit {
  readonly userId = input.required<string>();
  readonly closed = output<void>();

  private readonly api = inject(UserApiService);

  readonly user =
    signal<UserDetail | null>(null);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.loading.set(true);
    this.api.getById(this.userId()).subscribe({
      next: (detail) => {
        this.user.set(detail);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
```

```html
<!-- user-detail-panel.component.html -->
<div class="detail-panel">
  <div class="panel-header">
    <h2>User Details</h2>
    <button mat-icon-button
            (click)="closed.emit()"
            aria-label="Close panel">
      <mat-icon>close</mat-icon>
    </button>
  </div>

  @if (loading()) {
    <div class="skeleton-profile">
      <div class="skeleton-line"></div>
      <div class="skeleton-line"></div>
    </div>
  } @else if (user(); as u) {
    <div class="profile-section">
      <p><strong>Name:</strong> {{ u.name }}</p>
      <p><strong>Email:</strong> {{ u.email }}</p>
      <p><strong>Role:</strong> {{ u.role }}</p>
      <p>
        <strong>Status:</strong>
        <span class="status-badge"
              [class.active]="
                u.status === 'Active'"
              [class.inactive]="
                u.status === 'Inactive'">
          {{ u.status }}
        </span>
      </p>
      <p>
        <strong>Last Active:</strong>
        {{ u.lastActiveUtc
            | date:'medium' ?? 'Never' }}
      </p>
      <p>
        <strong>Created:</strong>
        {{ u.createdAtUtc | date:'mediumDate' }}
      </p>
    </div>

    <mat-divider></mat-divider>

    <app-activity-history
      [userId]="userId()">
    </app-activity-history>
  }
</div>
```

5. **Create `ActivityHistoryComponent`** for reverse-chronological activity log (AC-3):

```typescript
// client/src/app/features/admin/users/
//   activity-history.component.ts
import {
  Component, input, signal, inject, OnInit
} from '@angular/core';
import { MatListModule } from
  '@angular/material/list';
import { MatIconModule } from
  '@angular/material/icon';
import { MatButtonModule } from
  '@angular/material/button';
import { DatePipe } from '@angular/common';
import { UserApiService } from
  './user-api.service';
import { UserActivityEntry } from
  './models/user.models';

@Component({
  selector: 'app-activity-history',
  standalone: true,
  imports: [
    MatListModule, MatIconModule,
    MatButtonModule, DatePipe
  ],
  templateUrl:
    './activity-history.component.html'
})
export class ActivityHistoryComponent
    implements OnInit {
  readonly userId = input.required<string>();

  private readonly api = inject(UserApiService);

  readonly entries =
    signal<UserActivityEntry[]>([]);
  readonly loading = signal(false);
  readonly page = signal(1);
  readonly hasMore = signal(true);

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loading.set(true);
    this.api.getActivityHistory(
      this.userId(), this.page()
    ).subscribe({
      next: (data) => {
        this.entries.update(
          existing => [...existing, ...data]);
        this.hasMore.set(data.length === 25);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  loadMore(): void {
    this.page.update(p => p + 1);
    this.loadHistory();
  }

  getEventIcon(eventType: string): string {
    switch (eventType) {
      case 'Login': return 'login';
      case 'RoleChange': return 'swap_horiz';
      case 'StatusChange': return 'toggle_on';
      default: return 'history';
    }
  }
}
```

```html
<!-- activity-history.component.html -->
<h3>Activity History</h3>

@if (entries().length === 0 && !loading()) {
  <p class="no-activity">No activity recorded.</p>
} @else {
  <mat-list>
    @for (entry of entries(); track entry.id) {
      <mat-list-item>
        <mat-icon matListItemIcon>
          {{ getEventIcon(entry.eventType) }}
        </mat-icon>
        <span matListItemTitle>
          {{ entry.description }}
        </span>
        <span matListItemLine>
          {{ entry.occurredAtUtc
              | date:'medium' }}
          @if (entry.performedByName) {
            — by {{ entry.performedByName }}
          }
        </span>
      </mat-list-item>
    }
  </mat-list>

  @if (hasMore() && !loading()) {
    <button mat-button
            (click)="loadMore()"
            class="load-more-btn">
      Load More
    </button>
  }

  @if (loading()) {
    <p class="loading-text">Loading...</p>
  }
}
```

6. **Create dialog components** for bulk action confirmation and result summary:

```typescript
// client/src/app/features/admin/users/
//   bulk-action-confirm-dialog.component.ts
import { Component, inject } from '@angular/core';
import {
  MAT_DIALOG_DATA, MatDialogModule,
  MatDialogRef
} from '@angular/material/dialog';
import { MatButtonModule } from
  '@angular/material/button';
import { MatSelectModule } from
  '@angular/material/select';
import { MatFormFieldModule } from
  '@angular/material/form-field';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-bulk-action-confirm-dialog',
  standalone: true,
  imports: [
    MatDialogModule, MatButtonModule,
    MatSelectModule, MatFormFieldModule,
    FormsModule
  ],
  template: `
    <h2 mat-dialog-title>
      Confirm {{ data.action }}
    </h2>
    <mat-dialog-content>
      <p>
        Apply <strong>{{ data.action }}</strong>
        to {{ data.count }} selected user(s)?
      </p>
      @if (data.action === 'AssignRole') {
        <mat-form-field appearance="outline"
                        class="full-width">
          <mat-label>Target Role</mat-label>
          <mat-select [(ngModel)]="targetRole">
            <mat-option value="Admin">
              Admin
            </mat-option>
            <mat-option value="Staff">
              Staff
            </mat-option>
            <mat-option value="Clinician">
              Clinician
            </mat-option>
          </mat-select>
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button
              (click)="dialogRef.close(false)">
        Cancel
      </button>
      <button mat-raised-button
              [color]="data.action === 'Deactivate'
                ? 'warn' : 'primary'"
              [disabled]="data.action ===
                'AssignRole' && !targetRole"
              (click)="confirm()">
        {{ data.action }}
      </button>
    </mat-dialog-actions>
  `
})
export class BulkActionConfirmDialogComponent {
  readonly data = inject(MAT_DIALOG_DATA);
  readonly dialogRef = inject(
    MatDialogRef<
      BulkActionConfirmDialogComponent>);
  targetRole = '';

  confirm(): void {
    this.dialogRef.close({
      confirmed: true,
      targetRole: this.targetRole || undefined
    });
  }
}
```

```typescript
// client/src/app/features/admin/users/
//   bulk-action-result-dialog.component.ts
import { Component, inject } from '@angular/core';
import {
  MAT_DIALOG_DATA, MatDialogModule
} from '@angular/material/dialog';
import { MatButtonModule } from
  '@angular/material/button';
import { MatIconModule } from
  '@angular/material/icon';
import { MatListModule } from
  '@angular/material/list';

@Component({
  selector: 'app-bulk-action-result-dialog',
  standalone: true,
  imports: [
    MatDialogModule, MatButtonModule,
    MatIconModule, MatListModule
  ],
  template: `
    <h2 mat-dialog-title>
      Bulk Action Complete
    </h2>
    <mat-dialog-content>
      <p class="success-summary">
        <mat-icon color="primary">
          check_circle
        </mat-icon>
        {{ data.result.successCount }}
        user(s) {{ data.action | lowercase }}d
        successfully.
      </p>

      @if (data.result.failureCount > 0) {
        <p class="failure-summary">
          <mat-icon color="warn">warning</mat-icon>
          {{ data.result.failureCount }}
          user(s) failed:
        </p>
        <mat-list dense>
          @for (f of data.result.failures;
                track f.userId) {
            <mat-list-item>
              <mat-icon matListItemIcon
                        color="warn">
                error_outline
              </mat-icon>
              <span matListItemTitle>
                {{ f.userName }}
              </span>
              <span matListItemLine>
                {{ f.reason }}
              </span>
            </mat-list-item>
          }
        </mat-list>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-raised-button
              mat-dialog-close
              color="primary">
        Close
      </button>
    </mat-dialog-actions>
  `
})
export class BulkActionResultDialogComponent {
  readonly data = inject(MAT_DIALOG_DATA);
}
```

7. **Add lazy-loaded route** with admin guard:

```typescript
// In app.routes.ts
{
  path: 'admin/users',
  loadComponent: () =>
    import(
      './features/admin/users/' +
      'user-management.component'
    ).then(m => m.UserManagementComponent),
  canActivate: [adminGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── app.routes.ts                                      (modify)
            └── features/
                └── admin/
                    └── users/
                        ├── user-management.component.ts            (new)
                        ├── user-management.component.html          (new)
                        ├── user-management.component.scss          (new)
                        ├── user-detail-panel.component.ts          (new)
                        ├── user-detail-panel.component.html        (new)
                        ├── activity-history.component.ts           (new)
                        ├── activity-history.component.html         (new)
                        ├── bulk-action-confirm-dialog.component.ts (new)
                        ├── bulk-action-result-dialog.component.ts  (new)
                        ├── models/
                        │   └── user.models.ts                      (new)
                        └── user-api.service.ts                     (new)
```

> Placeholder: Update on execution based on US_061 task_001 and task_002 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/admin/users/models/user.models.ts | TypeScript interfaces for user list, detail, bulk action, activity entries |
| CREATE | client/src/app/features/admin/users/user-api.service.ts | HttpClient service for list, get, bulk action, activity history |
| CREATE | client/src/app/features/admin/users/user-management.component.ts | Page container with data table, search, filters, bulk selection, pagination |
| CREATE | client/src/app/features/admin/users/user-management.component.html | Table template with checkbox selection, bulk toolbar, mobile card layout |
| CREATE | client/src/app/features/admin/users/user-management.component.scss | Responsive styles for table/card layout, status badges, skeleton states |
| CREATE | client/src/app/features/admin/users/user-detail-panel.component.ts | Side panel with user profile details |
| CREATE | client/src/app/features/admin/users/user-detail-panel.component.html | Profile template with detail fields and activity history |
| CREATE | client/src/app/features/admin/users/activity-history.component.ts | Reverse chronological activity log with paginated loading |
| CREATE | client/src/app/features/admin/users/activity-history.component.html | Activity list with event icons, timestamps, performer names |
| CREATE | client/src/app/features/admin/users/bulk-action-confirm-dialog.component.ts | Confirmation dialog with role selector for AssignRole |
| CREATE | client/src/app/features/admin/users/bulk-action-result-dialog.component.ts | Result summary with success count and per-user failure details |
| MODIFY | client/src/app/app.routes.ts | Add /admin/users route with adminGuard |

## External References

- Angular Material Table with Selection: https://material.angular.io/components/table/overview
- Angular CDK SelectionModel: https://material.angular.io/cdk/collections/overview
- Angular Material Paginator: https://material.angular.io/components/paginator/overview
- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- Angular Material List: https://material.angular.io/components/list/overview
- WCAG 2.1 AA Data Tables: https://www.w3.org/WAI/tutorials/tables/
- Responsive Data Tables: https://adrianroselli.com/2017/11/a-responsive-accessible-table.html

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Test user management flow:
# 1. Log in as Admin
# 2. Navigate to /admin/users
# 3. Search by name → verify filter works
# 4. Select 3 users → click Deactivate → confirm
# 5. Verify result summary dialog shows count
# 6. Click a user row → verify side panel opens
# 7. Verify activity history shows entries
# 8. Try deactivating last admin → verify error
# 9. Resize to 375px → verify card layout
```

## Implementation Validation Strategy

- [ ] User table renders with name, email, role, status badge, last active and pagination (AC-1)
- [ ] Search by name/email filters table results (AC-1)
- [ ] Checkbox selection enables bulk toolbar with Activate, Deactivate, Assign Role buttons (AC-2)
- [ ] Confirmation dialog appears before bulk action execution (AC-2)
- [ ] Result dialog shows "{N} users {action}d" with failure details (AC-4)
- [ ] Activity history lists login, role change, status change events in reverse chronological order (AC-3)
- [ ] Last-admin guard error displayed when trying to deactivate all admins (edge case 1)
- [ ] Invalid role assignment errors displayed per-user in result dialog (edge case 2)
- [ ] Table switches to card layout on screens below 768px (UXR-303)
- [ ] Touch targets at least 44x44px on mobile (UXR-304)
- [ ] Text meets WCAG AA 4.5:1 contrast ratio (UXR-201)
- [ ] All interactive elements keyboard navigable (UXR-202)

## Implementation Checklist

- [ ] Create TypeScript interfaces for user list items, detail, bulk actions, results, and activity entries
- [ ] Implement UserApiService with HttpClient for list, get, bulk action, and activity history endpoints
- [ ] Build UserManagementComponent with data table, search, role/status filters, pagination, and checkbox selection
- [ ] Build bulk action toolbar with confirm dialog and result summary dialog
- [ ] Build UserDetailPanelComponent side panel with profile fields and activity history
- [ ] Build ActivityHistoryComponent with reverse chronological event list and load-more pagination
- [ ] Implement responsive layout: data table on desktop, card layout on mobile (< 768px)
- [ ] Add lazy-loaded route with adminGuard and register in app.routes.ts
