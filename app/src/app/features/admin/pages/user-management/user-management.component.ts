import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectionModel } from '@angular/cdk/collections';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import {
  MatPaginator,
  MatPaginatorModule,
  PageEvent,
} from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import {
  InviteStaffDialogComponent,
} from '../../components/invite-staff-dialog/invite-staff-dialog.component';
import { UserDetailPanelComponent } from './user-detail-panel.component';
import {
  BulkActionConfirmDialogComponent,
  BulkActionConfirmResult,
} from './bulk-action-confirm-dialog.component';
import { BulkActionResultDialogComponent } from './bulk-action-result-dialog.component';
import { UserApiService } from './user-api.service';
import { UserListItem, BulkActionTypeName } from './models/user.models';

/**
 * SCR-020: Admin User Management page (US_061).
 * Full-width data table with checkbox bulk-selection, role/status filters,
 * server-side pagination, user detail side panel, and activity history.
 *
 * AC-1: Paginated user list with search (name/email), role, and status filters.
 * AC-2: Bulk activate / deactivate / assign-role with confirmation dialog.
 * AC-3: User detail side panel with reverse-chronological activity history.
 * AC-4: Bulk result summary dialog showing success count + per-user failures.
 */
@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTableModule,
    UserDetailPanelComponent,
  ],
  templateUrl: './user-management.component.html',
  styleUrl: './user-management.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserManagementComponent implements OnInit {
  private readonly api = inject(UserApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  readonly displayedColumns = ['select', 'name', 'email', 'role', 'status', 'lastLoginAt'];

  readonly users = signal<UserListItem[]>([]);
  readonly totalCount = signal(0);
  readonly isLoading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly selectedUser = signal<UserListItem | null>(null);

  readonly selection = new SelectionModel<UserListItem>(true, []);

  searchTerm = '';
  roleFilter = '';
  statusFilter = '';
  pageSize = 25;
  pageIndex = 0;

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.isLoading.set(true);
    this.loadError.set(null);
    this.api
      .list(
        this.searchTerm || undefined,
        this.roleFilter || undefined,
        this.statusFilter || undefined,
        this.pageIndex + 1,
        this.pageSize,
      )
      .subscribe({
        next: (result) => {
          this.users.set(result.items);
          this.totalCount.set(result.totalCount);
          this.isLoading.set(false);
          this.selection.clear();
        },
        error: () => {
          this.loadError.set('Failed to load users. Please try again.');
          this.isLoading.set(false);
        },
      });
  }

  onSearch(): void {
    this.pageIndex = 0;
    this.loadUsers();
  }

  onFilterChange(): void {
    this.pageIndex = 0;
    this.loadUsers();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadUsers();
  }

  onRowClick(user: UserListItem): void {
    this.selectedUser.set(user);
  }

  closePanel(): void {
    this.selectedUser.set(null);
  }

  isAllSelected(): boolean {
    return (
      this.users().length > 0 &&
      this.selection.selected.length === this.users().length
    );
  }

  toggleAllRows(): void {
    if (this.isAllSelected()) {
      this.selection.clear();
    } else {
      this.selection.select(...this.users());
    }
  }

  statusLabel(isActive: boolean): string {
    return isActive ? 'Active' : 'Inactive';
  }

  /** Opens confirmation dialog then executes bulk action (AC-2, AC-4). */
  executeBulkAction(action: BulkActionTypeName): void {
    const confirmRef = this.dialog.open(BulkActionConfirmDialogComponent, {
      data: { action, count: this.selection.selected.length },
      width: '420px',
      disableClose: true,
    });

    confirmRef.afterClosed().subscribe((result: BulkActionConfirmResult | null) => {
      if (result === null) return; // cancelled

      this.api
        .bulkAction({
          userIds: this.selection.selected.map((u) => u.userId),
          action,
          targetRole: result.targetRole,
        })
        .subscribe({
          next: (bulkResult) => {
            this.dialog.open(BulkActionResultDialogComponent, {
              data: { result: bulkResult, action },
              width: '500px',
            });
            this.loadUsers();
          },
          error: (err: { error?: { message?: string } }) => {
            const msg = err.error?.message ?? 'Bulk action failed. Please try again.';
            this.snackBar.open(msg, 'Dismiss', { duration: 6000 });
          },
        });
    });
  }

  openInviteDialog(): void {
    const ref = this.dialog.open(InviteStaffDialogComponent, {
      width: '480px',
      disableClose: true,
    });

    ref.afterClosed().subscribe((sent: boolean) => {
      if (sent) {
        this.snackBar.open('Invitation sent successfully', 'Close', { duration: 4000 });
        this.loadUsers();
      }
    });
  }
}
