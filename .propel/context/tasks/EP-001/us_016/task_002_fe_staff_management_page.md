# Task - TASK_002

## Requirement Reference

- User Story: us_016
- Story Location: .propel/context/tasks/EP-001/us_016/us_016.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I submit a staff invitation with name, email, and role, Then an invitation email is sent to the specified address and a pending staff account is created with a 48-hour invitation expiry.
  - AC-2: Given a staff member receives an invitation email, When they click the invitation link and complete account setup (password), Then their account is activated with the assigned role and the activation event is recorded in the audit log.
  - AC-3: Given I am authenticated as an Admin, When I deactivate a staff account, Then all active sessions for that user are invalidated immediately and the account status is updated to inactive.
  - AC-4: Given a staff invitation link has expired (after 48 hours), When the invitee attempts to use it, Then the system displays "Invitation expired" and offers the Admin the option to resend.
- Edge Cases:
  - How does the system handle duplicate invitations to the same email? Second invitation resends the email and extends the expiry; duplicate accounts are not created.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-020-user-management.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | figma_spec.md#SCR-020 |
| **UXR Requirements** | UXR-111, UXR-201, UXR-202, UXR-205, UXR-501 |
| **Design Tokens** | designsystem.md — colors, typography, spacing |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | N/A (consumed via API) | N/A |
| Database | N/A | N/A |
| Library | Angular Material | 17.x |
| Library | Angular Reactive Forms | 17.x (bundled) |
| Library | @angular/router | 17.x (bundled) |
| Library | rxjs | 7.x |
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

Build the Angular 17 User Management page (SCR-020) and the standalone Staff Activation page. The User Management page provides a full-width data table of staff accounts with columns for name, email, role, status, and last active date; a search/filter toolbar; an Invite Staff dialog for sending new invitations (AC-1); and a confirmation dialog for deactivation actions (AC-3, UXR-111). The Staff Activation page handles the invitation link flow where invitees set their password to activate their account (AC-2), displays "Invitation expired" with a resend prompt for expired links (AC-4), and handles duplicate invitation resilience (Edge Case 2). Both pages follow SCR-020 state specifications (Default, Loading, Empty, Error, Validation) with responsive layout and accessibility compliance.

## Dependent Tasks

- US_001 tasks (requires Angular scaffold, routing shell, Material theme)
- US_014 task_002 (requires AuthService, AuthInterceptor, AuthGuard)
- US_015 task_001 (requires AdminOnly policy — frontend guards Admin routes)
- task_001_be_staff_lifecycle_api (requires backend API endpoints)

## Impacted Components

- New: `client/src/app/features/admin/pages/user-management/user-management.component.ts` (user management page)
- New: `client/src/app/features/admin/pages/user-management/user-management.component.html` (template)
- New: `client/src/app/features/admin/pages/user-management/user-management.component.scss` (styles)
- New: `client/src/app/features/admin/components/invite-staff-dialog/invite-staff-dialog.component.ts` (invite dialog)
- New: `client/src/app/features/admin/components/invite-staff-dialog/invite-staff-dialog.component.html` (dialog template)
- New: `client/src/app/features/admin/components/deactivate-confirm-dialog/deactivate-confirm-dialog.component.ts` (confirmation dialog)
- New: `client/src/app/features/auth/pages/activate/activate.component.ts` (staff activation page)
- New: `client/src/app/features/auth/pages/activate/activate.component.html` (activation template)
- New: `client/src/app/features/auth/pages/activate/activate.component.scss` (activation styles)
- New: `client/src/app/features/admin/services/staff-management.service.ts` (HTTP client for staff API)
- Modify: `client/src/app/features/admin/admin-routing.module.ts` (add user-management route)
- Modify: `client/src/app/features/auth/auth-routing.module.ts` (add activate route)

## Implementation Plan

1. **Create `StaffManagementService`** as the HTTP client for all staff lifecycle API calls:

```typescript
// client/src/app/features/admin/services/staff-management.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface InviteStaffRequest {
  fullName: string;
  email: string;
  role: string;
}

export interface InviteStaffResponse {
  userId: string;
  email: string;
  status: string;
  invitationExpiresAt: string;
}

export interface ActivateStaffRequest {
  token: string;
  email: string;
  password: string;
}

export interface StaffListItem {
  id: string;
  fullName: string;
  email: string;
  role: string;
  accountStatus: string;
  lastActive: string | null;
  invitedAt: string | null;
  activatedAt: string | null;
}

export interface StaffListResponse {
  items: StaffListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class StaffManagementService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/v1/admin/staff`;

  getStaffList(
    page = 1,
    pageSize = 25,
    status?: string,
    search?: string,
  ): Observable<StaffListResponse> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    if (search) params = params.set('search', search);
    return this.http.get<StaffListResponse>(this.apiUrl, { params });
  }

  inviteStaff(request: InviteStaffRequest): Observable<InviteStaffResponse> {
    return this.http.post<InviteStaffResponse>(
      `${this.apiUrl}/invite`, request);
  }

  deactivateStaff(userId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/${userId}/deactivate`, {});
  }

  activateStaff(
    request: ActivateStaffRequest,
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/activate`, request);
  }
}
```

2. **Create the `UserManagementComponent`** with `MatTable`, `MatPaginator`, `MatSort`, search, and status filter. Implements SCR-020 states (Default, Loading, Empty, Error, Validation):

```typescript
// client/src/app/features/admin/pages/user-management/
//   user-management.component.ts
import {
  Component, ChangeDetectionStrategy, signal, ViewChild,
  AfterViewInit,
} from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FormsModule } from '@angular/forms';
import {
  StaffManagementService, StaffListItem,
} from '../../services/staff-management.service';
import {
  InviteStaffDialogComponent,
} from '../../components/invite-staff-dialog/invite-staff-dialog.component';
import {
  DeactivateConfirmDialogComponent,
} from '../../components/deactivate-confirm-dialog/deactivate-confirm-dialog.component';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [
    MatTableModule, MatPaginatorModule, MatSortModule,
    MatInputModule, MatSelectModule, MatButtonModule,
    MatIconModule, MatChipsModule, MatProgressBarModule,
    MatDialogModule, MatSnackBarModule, FormsModule,
  ],
  templateUrl: './user-management.component.html',
  styleUrls: ['./user-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserManagementComponent implements AfterViewInit {
  displayedColumns = [
    'fullName', 'email', 'role', 'accountStatus',
    'lastActive', 'actions',
  ];

  staffList = signal<StaffListItem[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  loadError = signal<string | null>(null);
  searchTerm = '';
  statusFilter = '';
  pageSize = 25;
  pageIndex = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    private staffService: StaffManagementService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
  ) {}

  ngAfterViewInit(): void {
    this.loadStaff();
  }

  loadStaff(): void {
    this.isLoading.set(true);
    this.loadError.set(null);

    this.staffService.getStaffList(
      this.pageIndex + 1, this.pageSize,
      this.statusFilter || undefined,
      this.searchTerm || undefined,
    ).subscribe({
      next: (response) => {
        this.staffList.set(response.items);
        this.totalCount.set(response.totalCount);
        this.isLoading.set(false);
      },
      error: () => {
        this.loadError.set('Failed to load staff list. Please retry.');
        this.isLoading.set(false);
      },
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadStaff();
  }

  onSearch(): void {
    this.pageIndex = 0;
    this.loadStaff();
  }

  onStatusFilterChange(): void {
    this.pageIndex = 0;
    this.loadStaff();
  }

  openInviteDialog(): void {
    const dialogRef = this.dialog.open(InviteStaffDialogComponent, {
      width: '480px',
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.snackBar.open('Invitation sent successfully', 'Close', {
          duration: 4000,
        });
        this.loadStaff();
      }
    });
  }

  confirmDeactivate(user: StaffListItem): void {
    const dialogRef = this.dialog.open(DeactivateConfirmDialogComponent, {
      width: '400px',
      data: { userName: user.fullName, userId: user.id },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.staffService.deactivateStaff(user.id).subscribe({
          next: () => {
            this.snackBar.open('Account deactivated', 'Close', {
              duration: 4000,
            });
            this.loadStaff();
          },
          error: (err) => {
            const detail = err.error?.detail
              ?? 'Failed to deactivate account.';
            this.snackBar.open(detail, 'Close', { duration: 5000 });
          },
        });
      }
    });
  }
}
```

3. **Create the User Management template** with SCR-020 states:

```html
<!-- client/src/app/features/admin/pages/user-management/
     user-management.component.html -->
<div class="user-management" role="main"
     aria-label="User Management">
  <header class="page-header">
    <h1>User Management</h1>
    <button mat-raised-button color="primary"
            (click)="openInviteDialog()"
            aria-label="Invite new staff member">
      <mat-icon>person_add</mat-icon>
      Invite Staff
    </button>
  </header>

  <!-- Toolbar: Search + Filter -->
  <div class="toolbar">
    <mat-form-field appearance="outline" class="search-field">
      <mat-label>Search by name or email</mat-label>
      <input matInput [(ngModel)]="searchTerm"
             (keyup.enter)="onSearch()"
             aria-label="Search staff members" />
      <mat-icon matSuffix>search</mat-icon>
    </mat-form-field>

    <mat-form-field appearance="outline" class="filter-field">
      <mat-label>Status</mat-label>
      <mat-select [(ngModel)]="statusFilter"
                  (selectionChange)="onStatusFilterChange()">
        <mat-option value="">All</mat-option>
        <mat-option value="Pending">Pending</mat-option>
        <mat-option value="Active">Active</mat-option>
        <mat-option value="Inactive">Inactive</mat-option>
      </mat-select>
    </mat-form-field>
  </div>

  <!-- Loading state -->
  @if (isLoading()) {
    <mat-progress-bar mode="indeterminate"
                      aria-label="Loading staff list">
    </mat-progress-bar>
  }

  <!-- Error state -->
  @if (loadError()) {
    <div class="error-banner" role="alert" aria-live="polite">
      <span>{{ loadError() }}</span>
      <button mat-button color="primary" (click)="loadStaff()">
        Retry
      </button>
    </div>
  }

  <!-- Empty state -->
  @if (!isLoading() && !loadError() && staffList().length === 0) {
    <div class="empty-state" role="status">
      <mat-icon class="empty-icon">group_off</mat-icon>
      <p>No users found</p>
      <button mat-raised-button color="primary"
              (click)="openInviteDialog()">
        Invite Your First Staff Member
      </button>
    </div>
  }

  <!-- Default state: Data table -->
  @if (!isLoading() && !loadError() && staffList().length > 0) {
    <table mat-table [dataSource]="staffList()" matSort
           class="staff-table" aria-label="Staff accounts table">

      <ng-container matColumnDef="fullName">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th>
        <td mat-cell *matCellDef="let row">{{ row.fullName }}</td>
      </ng-container>

      <ng-container matColumnDef="email">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>Email</th>
        <td mat-cell *matCellDef="let row">{{ row.email }}</td>
      </ng-container>

      <ng-container matColumnDef="role">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>Role</th>
        <td mat-cell *matCellDef="let row">
          <mat-chip-option [selectable]="false" [color]="'primary'">
            {{ row.role }}
          </mat-chip-option>
        </td>
      </ng-container>

      <ng-container matColumnDef="accountStatus">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
        <td mat-cell *matCellDef="let row">
          <span class="status-badge"
                [class.active]="row.accountStatus === 'Active'"
                [class.pending]="row.accountStatus === 'Pending'"
                [class.inactive]="row.accountStatus === 'Inactive'">
            {{ row.accountStatus }}
          </span>
        </td>
      </ng-container>

      <ng-container matColumnDef="lastActive">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>
          Last Active
        </th>
        <td mat-cell *matCellDef="let row">
          {{ row.lastActive ? (row.lastActive | date:'medium') : '—' }}
        </td>
      </ng-container>

      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>Actions</th>
        <td mat-cell *matCellDef="let row">
          @if (row.accountStatus === 'Active') {
            <button mat-icon-button color="warn"
                    (click)="confirmDeactivate(row)"
                    [attr.aria-label]="'Deactivate ' + row.fullName">
              <mat-icon>block</mat-icon>
            </button>
          }
          @if (row.accountStatus === 'Pending') {
            <button mat-icon-button color="primary"
                    (click)="resendInvitation(row)"
                    [attr.aria-label]="'Resend invitation to '
                      + row.fullName">
              <mat-icon>send</mat-icon>
            </button>
          }
        </td>
      </ng-container>

      <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
      <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
    </table>

    <mat-paginator [length]="totalCount()"
                   [pageSize]="pageSize"
                   [pageIndex]="pageIndex"
                   [pageSizeOptions]="[10, 25, 50]"
                   (page)="onPageChange($event)"
                   showFirstLastButtons>
    </mat-paginator>
  }
</div>
```

4. **Create responsive SCSS** for the User Management page following SCR-020 layout:

```scss
// client/src/app/features/admin/pages/user-management/
//   user-management.component.scss
.user-management {
  padding: 24px;
  max-width: 1440px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;

  h1 { font-size: 1.5rem; font-weight: 600; margin: 0; }

  button { min-height: 44px; min-width: 44px; }
}

.toolbar {
  display: flex;
  gap: 16px;
  margin-bottom: 16px;

  .search-field { flex: 1; max-width: 400px; }
  .filter-field { width: 180px; }
}

.staff-table { width: 100%; }

.status-badge {
  display: inline-block;
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;

  &.active { background-color: #e8f5e9; color: #2e7d32; }
  &.pending { background-color: #fff3e0; color: #ef6c00; }
  &.inactive { background-color: #fbe9e7; color: #c62828; }
}

.error-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background-color: #fdecea;
  border: 1px solid #f5c6cb;
  border-radius: 4px;
  margin-bottom: 16px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 64px 24px;
  text-align: center;

  .empty-icon { font-size: 48px; height: 48px; width: 48px; opacity: 0.4; }
  p { margin: 16px 0 24px; font-size: 1rem; color: rgba(0, 0, 0, 0.54); }
}

// UXR-301: Responsive breakpoints
@media (max-width: 768px) {
  .page-header { flex-direction: column; gap: 12px; align-items: stretch; }
  .toolbar { flex-direction: column; }
  .toolbar .search-field { max-width: 100%; }
  .toolbar .filter-field { width: 100%; }
}

@media (max-width: 375px) {
  .user-management { padding: 12px; }
  .page-header h1 { font-size: 1.25rem; }
}
```

5. **Create the `InviteStaffDialogComponent`** with reactive form for name, email, and role selection (AC-1):

```typescript
// client/src/app/features/admin/components/invite-staff-dialog/
//   invite-staff-dialog.component.ts
import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import {
  FormGroup, FormControl, Validators, ReactiveFormsModule,
} from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { StaffManagementService } from '../../services/staff-management.service';

@Component({
  selector: 'app-invite-staff-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatDialogModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>Invite Staff Member</h2>
    <mat-dialog-content>
      <form [formGroup]="inviteForm" class="invite-form">
        @if (serverError()) {
          <div class="server-error" role="alert">{{ serverError() }}</div>
        }
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Full Name</mat-label>
          <input matInput formControlName="fullName"
                 autocomplete="name"
                 [attr.aria-describedby]="inviteForm.controls.fullName
                   .invalid && inviteForm.controls.fullName.touched
                   ? 'name-error' : null" />
          @if (inviteForm.controls.fullName.touched
               && inviteForm.controls.fullName.hasError('required')) {
            <mat-error id="name-error">Name is required</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email Address</mat-label>
          <input matInput formControlName="email" type="email"
                 autocomplete="email"
                 [attr.aria-describedby]="inviteForm.controls.email
                   .invalid && inviteForm.controls.email.touched
                   ? 'email-error' : null" />
          @if (inviteForm.controls.email.touched
               && inviteForm.controls.email.invalid) {
            <mat-error id="email-error">Valid email is required</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">
            <mat-option value="Staff">Staff</mat-option>
            <mat-option value="Clinician">Clinician</mat-option>
            <mat-option value="Admin">Admin</mat-option>
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button color="primary"
              [disabled]="inviteForm.invalid || isSubmitting()"
              (click)="onSubmit()">
        @if (isSubmitting()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          Send Invitation
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .invite-form { display: flex; flex-direction: column; gap: 8px; }
    .full-width { width: 100%; }
    .server-error {
      color: #f44336; background: #fdecea;
      border: 1px solid #f5c6cb; border-radius: 4px;
      padding: 8px 12px; margin-bottom: 8px; font-size: 0.875rem;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InviteStaffDialogComponent {
  inviteForm = new FormGroup({
    fullName: new FormControl('', [
      Validators.required, Validators.maxLength(200),
    ]),
    email: new FormControl('', [
      Validators.required, Validators.email,
    ]),
    role: new FormControl('Staff', [Validators.required]),
  });

  isSubmitting = signal(false);
  serverError = signal<string | null>(null);

  constructor(
    private dialogRef: MatDialogRef<InviteStaffDialogComponent>,
    private staffService: StaffManagementService,
  ) {}

  onSubmit(): void {
    if (this.inviteForm.invalid) return;

    this.isSubmitting.set(true);
    this.serverError.set(null);

    const { fullName, email, role } = this.inviteForm.value;

    this.staffService.inviteStaff({
      fullName: fullName!, email: email!, role: role!,
    }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.serverError.set(
          err.error?.detail ?? 'Failed to send invitation.');
      },
    });
  }
}
```

6. **Create the `DeactivateConfirmDialogComponent`** with confirmation prompt (UXR-111):

```typescript
// client/src/app/features/admin/components/deactivate-confirm-dialog/
//   deactivate-confirm-dialog.component.ts
import { Component, Inject, ChangeDetectionStrategy } from '@angular/core';
import {
  MAT_DIALOG_DATA, MatDialogRef, MatDialogModule,
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-deactivate-confirm-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Confirm Deactivation</h2>
    <mat-dialog-content>
      <p>Are you sure you want to deactivate
        <strong>{{ data.userName }}</strong>?</p>
      <p class="warning-text">
        All active sessions will be terminated immediately.
        This action can be reversed by reactivating the account.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button color="warn"
              [mat-dialog-close]="true">
        Deactivate
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .warning-text {
      color: rgba(0, 0, 0, 0.54);
      font-size: 0.875rem;
      margin-top: 8px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeactivateConfirmDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<DeactivateConfirmDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: {
      userName: string;
      userId: string;
    },
  ) {}
}
```

7. **Create the `ActivateComponent`** as the standalone page for invitation link handling (AC-2, AC-4):

```typescript
// client/src/app/features/auth/pages/activate/activate.component.ts
import { Component, OnInit, ChangeDetectionStrategy, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  FormGroup, FormControl, Validators, ReactiveFormsModule,
} from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { StaffManagementService } from '../../../admin/services/staff-management.service';

@Component({
  selector: 'app-activate',
  standalone: true,
  imports: [
    ReactiveFormsModule, MatInputModule, MatButtonModule,
    MatIconModule, MatProgressSpinnerModule, RouterLink,
  ],
  templateUrl: './activate.component.html',
  styleUrls: ['./activate.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActivateComponent implements OnInit {
  activateForm = new FormGroup({
    password: new FormControl('', [
      Validators.required,
      Validators.minLength(12),
    ]),
    confirmPassword: new FormControl('', [Validators.required]),
  });

  email = '';
  token = '';
  isSubmitting = signal(false);
  isExpired = signal(false);
  isActivated = signal(false);
  serverError = signal<string | null>(null);
  showPassword = signal(false);

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private staffService: StaffManagementService,
  ) {}

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParams['email'] ?? '';
    this.token = this.route.snapshot.queryParams['token'] ?? '';
  }

  togglePasswordVisibility(): void {
    this.showPassword.update(v => !v);
  }

  onSubmit(): void {
    if (this.activateForm.invalid) return;

    const { password, confirmPassword } = this.activateForm.value;
    if (password !== confirmPassword) {
      this.serverError.set('Passwords do not match.');
      return;
    }

    this.isSubmitting.set(true);
    this.serverError.set(null);

    this.staffService.activateStaff({
      token: this.token,
      email: this.email,
      password: password!,
    }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.isActivated.set(true);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const detail = err.error?.detail ?? '';
        if (detail.includes('expired')) {
          this.isExpired.set(true);
        } else {
          this.serverError.set(
            detail || 'Activation failed. Please try again.');
        }
      },
    });
  }
}
```

Activation template:
```html
<!-- client/src/app/features/auth/pages/activate/activate.component.html -->
<div class="activate-container" role="main"
     aria-label="Account Activation">
  <header class="activate-header">
    <img src="assets/logo.svg" alt="PropelIQ Logo" class="logo" />
    <h1>Activate Your Account</h1>
  </header>

  <!-- AC-4: Expired invitation state -->
  @if (isExpired()) {
    <div class="expired-state" role="alert">
      <mat-icon class="expired-icon">timer_off</mat-icon>
      <h2>Invitation Expired</h2>
      <p>This invitation link has expired after 48 hours.</p>
      <p>Please contact your administrator to resend the invitation.</p>
      <a mat-raised-button color="primary" routerLink="/auth/login">
        Go to Login
      </a>
    </div>
  }

  <!-- Success state -->
  @if (isActivated()) {
    <div class="success-state" role="status">
      <mat-icon class="success-icon">check_circle</mat-icon>
      <h2>Account Activated</h2>
      <p>Your account has been activated successfully.</p>
      <a mat-raised-button color="primary" routerLink="/auth/login">
        Log In
      </a>
    </div>
  }

  <!-- Activation form -->
  @if (!isExpired() && !isActivated()) {
    <form [formGroup]="activateForm" (ngSubmit)="onSubmit()" novalidate>
      @if (serverError()) {
        <div class="server-error" role="alert" aria-live="polite">
          {{ serverError() }}
        </div>
      }

      <p class="activate-email">
        Activating account for: <strong>{{ email }}</strong>
      </p>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Password</mat-label>
        <input matInput formControlName="password"
               [type]="showPassword() ? 'text' : 'password'"
               autocomplete="new-password" />
        <button mat-icon-button matSuffix type="button"
                (click)="togglePasswordVisibility()"
                [attr.aria-label]="showPassword()
                  ? 'Hide password' : 'Show password'">
          <mat-icon>{{ showPassword()
            ? 'visibility_off' : 'visibility' }}</mat-icon>
        </button>
        @if (activateForm.controls.password.touched
             && activateForm.controls.password.invalid) {
          <mat-error>Minimum 12 characters required</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Confirm Password</mat-label>
        <input matInput formControlName="confirmPassword"
               type="password" autocomplete="new-password" />
      </mat-form-field>

      <button mat-raised-button color="primary" type="submit"
              [disabled]="activateForm.invalid || isSubmitting()"
              class="submit-btn">
        @if (isSubmitting()) {
          <mat-spinner diameter="20"></mat-spinner>
        } @else {
          Activate Account
        }
      </button>
    </form>
  }
</div>
```

8. **Register routes** for user management (admin-guarded) and activation (public):

```typescript
// In admin-routing.module.ts
{
  path: 'users',
  loadComponent: () => import(
    './pages/user-management/user-management.component')
    .then(m => m.UserManagementComponent),
  title: 'User Management — PropelIQ',
}

// In auth-routing.module.ts
{
  path: 'activate',
  loadComponent: () => import(
    './pages/activate/activate.component')
    .then(m => m.ActivateComponent),
  title: 'Activate Account — PropelIQ',
}
```

## Current Project State

```text
propelIQ/
├── client/
│   └── src/
│       ├── app/
│       │   ├── app.config.ts                (from US_001)
│       │   ├── core/
│       │   │   ├── interceptors/
│       │   │   │   └── auth.interceptor.ts  (from US_014 task_002)
│       │   │   └── guards/
│       │   │       └── auth.guard.ts        (from US_014 task_002)
│       │   └── features/
│       │       ├── auth/
│       │       │   ├── pages/
│       │       │   │   ├── login/           (from US_014 task_002)
│       │       │   │   ├── register/        (from US_013 task_002)
│       │       │   │   └── activate/        (NEW)
│       │       │   ├── services/
│       │       │   │   └── auth.service.ts  (from US_013, US_014)
│       │       │   └── auth-routing.module.ts
│       │       └── admin/
│       │           ├── pages/
│       │           │   └── user-management/ (NEW)
│       │           ├── components/
│       │           │   ├── invite-staff-dialog/  (NEW)
│       │           │   └── deactivate-confirm-dialog/ (NEW)
│       │           ├── services/
│       │           │   └── staff-management.service.ts (NEW)
│       │           └── admin-routing.module.ts
│       └── assets/
└── server/                                  (from US_001)
```

> Placeholder: Update on execution based on dependent task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/admin/services/staff-management.service.ts | HTTP client with getStaffList, inviteStaff, deactivateStaff, activateStaff |
| CREATE | client/src/app/features/admin/pages/user-management/user-management.component.ts | Data table with search, filter, pagination, invite dialog, deactivate action |
| CREATE | client/src/app/features/admin/pages/user-management/user-management.component.html | SCR-020 template with 5 states: Default, Loading, Empty, Error, Validation |
| CREATE | client/src/app/features/admin/pages/user-management/user-management.component.scss | Responsive styles for 375px/768px/1440px breakpoints |
| CREATE | client/src/app/features/admin/components/invite-staff-dialog/invite-staff-dialog.component.ts | Dialog with name, email, role form and loading spinner (UXR-501) |
| CREATE | client/src/app/features/admin/components/deactivate-confirm-dialog/deactivate-confirm-dialog.component.ts | Confirmation dialog for destructive deactivation action (UXR-111) |
| CREATE | client/src/app/features/auth/pages/activate/activate.component.ts | Activation page with password setup, expired state, success state |
| CREATE | client/src/app/features/auth/pages/activate/activate.component.html | Activation template with form, expired banner, success message |
| CREATE | client/src/app/features/auth/pages/activate/activate.component.scss | Centered single-column activation layout |
| MODIFY | client/src/app/features/admin/admin-routing.module.ts | Add /admin/users route with lazy-loaded UserManagementComponent |
| MODIFY | client/src/app/features/auth/auth-routing.module.ts | Add /auth/activate route with lazy-loaded ActivateComponent |

## External References

- Angular Material table: https://material.angular.io/components/table/overview
- Angular Material paginator: https://material.angular.io/components/paginator/overview
- Angular Material dialog: https://material.angular.io/components/dialog/overview
- Angular Material snack-bar: https://material.angular.io/components/snack-bar/overview
- Angular reactive forms: https://angular.dev/guide/forms/reactive-forms
- Angular signals: https://angular.dev/guide/signals
- WCAG 2.1 AA data tables: https://www.w3.org/WAI/tutorials/tables/

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve with hot reload
ng serve --open

# Navigate to user management
# http://localhost:4200/admin/users

# Navigate to activation page
# http://localhost:4200/auth/activate?email=...&token=...

# Run unit tests
ng test --watch=false

# Lint
ng lint
```

## Implementation Validation Strategy

- [x] User Management page renders data table with staff list per SCR-020 Default state
- [x] Loading state shows progress bar during API fetch
- [x] Empty state shows "No users found" with invite CTA
- [x] Error state shows retry banner on API failure
- [x] Invite dialog opens, validates inputs, sends invitation, shows success snackbar (AC-1)
- [x] Deactivate confirmation dialog requires explicit confirmation before proceeding (UXR-111, AC-3)
- [x] Self-deactivation error message is displayed from backend response (Edge-1)
- [x] Activation page extracts email/token from URL, allows password setup (AC-2)
- [x] Expired invitation shows "Invitation expired" state with admin contact prompt (AC-4)
- [x] Successful activation shows success state with login redirect
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

## Implementation Checklist

- [x] Create `StaffManagementService` with getStaffList, inviteStaff, deactivateStaff, activateStaff methods
- [x] Create `UserManagementComponent` with MatTable, MatPaginator, search field, status filter, and all 5 SCR-020 states
- [x] Create `InviteStaffDialogComponent` with reactive form for name, email, role and loading spinner (UXR-501)
- [x] Create `DeactivateConfirmDialogComponent` with confirmation prompt for destructive action (UXR-111)
- [x] Create `ActivateComponent` with password setup form, expired state (AC-4), and success state (AC-2)
- [x] Create responsive SCSS for user management and activation pages with 375/768/1440px breakpoints
- [x] Register routes: /admin/users (guarded) and /auth/activate (public) with lazy loading
- [ ] **[UI Tasks - MANDATORY]** Reference wireframe from Design References table during implementation
