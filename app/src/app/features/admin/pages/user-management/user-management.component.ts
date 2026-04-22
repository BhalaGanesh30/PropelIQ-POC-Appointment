import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
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
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import {
  DeactivateConfirmDialogComponent,
} from '../../components/deactivate-confirm-dialog/deactivate-confirm-dialog.component';
import {
  InviteStaffDialogComponent,
} from '../../components/invite-staff-dialog/invite-staff-dialog.component';
import {
  StaffListItem,
  StaffManagementService,
} from '../../services/staff-management.service';

/**
 * SCR-020: Admin User Management page.
 * Provides a full-width data table of staff accounts with search, status
 * filter, pagination, and actions: Invite (AC-1), Deactivate (AC-3),
 * Resend invitation (Edge-2).
 */
@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSnackBarModule,
    MatSortModule,
    MatTableModule,
  ],
  templateUrl: './user-management.component.html',
  styleUrl: './user-management.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserManagementComponent implements AfterViewInit {
  private readonly staffService = inject(StaffManagementService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  readonly displayedColumns = [
    'fullName',
    'email',
    'role',
    'accountStatus',
    'invitedAt',
    'actions',
  ];

  readonly staffList = signal<StaffListItem[]>([]);
  readonly totalCount = signal(0);
  readonly isLoading = signal(false);
  readonly loadError = signal<string | null>(null);

  searchTerm = '';
  statusFilter = '';
  pageSize = 25;
  pageIndex = 0;

  ngAfterViewInit(): void {
    this.loadStaff();
  }

  loadStaff(): void {
    this.isLoading.set(true);
    this.loadError.set(null);

    this.staffService
      .getStaffList(
        this.pageIndex + 1,
        this.pageSize,
        this.statusFilter || undefined,
        this.searchTerm || undefined,
      )
      .subscribe({
        next: (response) => {
          this.staffList.set(response.items);
          this.totalCount.set(response.totalCount);
          this.isLoading.set(false);
        },
        error: () => {
          this.loadError.set('Failed to load staff list. Please try again.');
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
    const ref = this.dialog.open(InviteStaffDialogComponent, {
      width: '480px',
      disableClose: true,
    });

    ref.afterClosed().subscribe((sent: boolean) => {
      if (sent) {
        this.snackBar.open('Invitation sent successfully', 'Close', {
          duration: 4000,
        });
        this.loadStaff();
      }
    });
  }

  /** Resend invitation for Pending accounts (Edge-2 — extends expiry). */
  resendInvitation(user: StaffListItem): void {
    const ref = this.dialog.open(InviteStaffDialogComponent, {
      width: '480px',
      disableClose: true,
      data: { prefillEmail: user.email, prefillName: user.fullName },
    });

    ref.afterClosed().subscribe((sent: boolean) => {
      if (sent) {
        this.snackBar.open('Invitation resent successfully', 'Close', {
          duration: 4000,
        });
        this.loadStaff();
      }
    });
  }

  confirmDeactivate(user: StaffListItem): void {
    const ref = this.dialog.open(DeactivateConfirmDialogComponent, {
      width: '420px',
      data: { userName: user.fullName, userId: user.id },
    });

    ref.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) return;

      this.staffService.deactivateStaff(user.id).subscribe({
        next: () => {
          this.snackBar.open('Account deactivated', 'Close', { duration: 4000 });
          this.loadStaff();
        },
        error: (err: { error?: { detail?: string } }) => {
          const msg = err.error?.detail ?? 'Failed to deactivate account.';
          this.snackBar.open(msg, 'Close', { duration: 5000 });
        },
      });
    });
  }
}
