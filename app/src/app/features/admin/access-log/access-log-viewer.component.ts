import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatNativeDateModule } from '@angular/material/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { AccessLogApiService } from './access-log-api.service';
import { AccessLogEntry } from '../../settings/disclosure/models/disclosure.models';

/**
 * Admin Access Log Viewer (US_057, AC-4).
 *
 * Route: /admin/access-logs
 * Policy: StaffOrAdmin (enforced via roleGuard on the route)
 *
 * Allows admin and staff users to query the access log for a specific patient
 * by patient ID with optional date range filtering.  Results are paginated and
 * displayed in chronological order (oldest first), matching the backend sort
 * order from AccessLogController.
 *
 * States: Loading, Empty, Error, Results table.
 * UXR-303: Card layout below 768 px.
 */
@Component({
  selector: 'app-access-log-viewer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    DatePipe,
    MatButtonModule,
    MatChipsModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatNativeDateModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  templateUrl: './access-log-viewer.component.html',
  styleUrl: './access-log-viewer.component.scss',
})
export class AccessLogViewerComponent {
  private readonly api = inject(AccessLogApiService);

  // ── Filter state ──────────────────────────────────────────────────────────
  readonly patientIdInput = signal('');
  readonly fromDate       = signal<Date | null>(null);
  readonly toDate         = signal<Date | null>(null);

  readonly today = new Date();

  // ── Results state ─────────────────────────────────────────────────────────
  readonly entries      = signal<AccessLogEntry[]>([]);
  readonly totalCount   = signal(0);
  readonly loading      = signal(false);
  readonly errorMessage = signal('');
  readonly hasQueried   = signal(false);

  // ── Pagination ────────────────────────────────────────────────────────────
  readonly page     = signal(1);
  readonly pageSize = signal(20);

  readonly displayedColumns = ['occurredAt', 'actorName', 'actorRole', 'resourceType', 'entityId'];

  // ── Query ─────────────────────────────────────────────────────────────────

  get canSearch(): boolean {
    return this.patientIdInput().trim().length > 0;
  }

  search(): void {
    if (!this.canSearch) return;
    this.page.set(1);
    this.loadEntries();
  }

  onPageChange(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.loadEntries();
  }

  clearFilters(): void {
    this.patientIdInput.set('');
    this.fromDate.set(null);
    this.toDate.set(null);
    this.entries.set([]);
    this.totalCount.set(0);
    this.errorMessage.set('');
    this.hasQueried.set(false);
  }

  private loadEntries(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.hasQueried.set(true);

    this.api
      .query({
        patientId: this.patientIdInput().trim(),
        fromUtc:   this.fromDate() ? this.fromDate()!.toISOString() : undefined,
        toUtc:     this.toDate()   ? this.toDate()!.toISOString()   : undefined,
        page:      this.page(),
        pageSize:  this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.entries.set(result.items);
          this.totalCount.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.errorMessage.set('Failed to load access log. Please check the patient ID and try again.');
          this.loading.set(false);
        },
      });
  }
}
