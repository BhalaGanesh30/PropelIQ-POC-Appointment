import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { debounceTime, distinctUntilChanged, filter } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { KpiApiService } from './kpi-api.service';
import { KpiCardComponent } from './kpi-card.component';
import { DistributionConfigDialogComponent } from './distribution-config-dialog.component';
import { KpiCardValue } from './models/kpi.models';

/**
 * SCR-023 KPI Dashboard page container (US_060, AC-1–AC-4, edge cases 1–2).
 *
 * Route:  /admin/kpi
 * Guard:  roleGuard [Admin]
 *
 * States:
 * - Loading: skeleton grid of 4 placeholder cards.
 * - Empty:   "Insufficient data" panel (edge case 2 — all values zero).
 * - Default: 2–4 column responsive grid of KpiCardComponent widgets.
 * - Error:   per-widget retry in KpiCardComponent; snackbar for summary failure.
 * - Validation: date-range-applied indicator below grid.
 *
 * Edge case 1 — stale data: staleness warning banner with "Last updated" timestamp.
 * Edge case 2 — empty period: empty-state panel with date-range suggestion.
 */
@Component({
  selector: 'app-kpi-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSnackBarModule,
    KpiCardComponent,
  ],
  templateUrl: './kpi-dashboard.component.html',
  styleUrl: './kpi-dashboard.component.scss',
})
export class KpiDashboardComponent {
  private readonly api      = inject(KpiApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog   = inject(MatDialog);

  // ── Date range form ────────────────────────────────────────────────────────
  readonly dateRange = new FormGroup({
    start: new FormControl<Date | null>(this.defaultFrom()),
    end:   new FormControl<Date | null>(this.defaultTo()),
  });

  // ── Signals ────────────────────────────────────────────────────────────────
  /** ISO date strings passed to KpiCardComponent inputs. */
  readonly fromDate    = signal<string>(toIsoDate(this.defaultFrom()));
  readonly toDate      = signal<string>(toIsoDate(this.defaultTo()));
  readonly cards       = signal<KpiCardValue[]>([]);
  readonly loading     = signal(false);
  readonly isStale     = signal(false);
  readonly computedAt  = signal<string>('');
  /** True when at least one card has a non-zero value (edge case 2 guard). */
  readonly hasData     = signal(true);

  ngOnInit(): void {
    this.loadDashboard();

    // Re-load when both start and end are selected (AC-2: update within 1 s).
    this.dateRange.valueChanges.pipe(
      debounceTime(200),
      distinctUntilChanged(),
      filter(v => v.start instanceof Date && v.end instanceof Date),
    ).subscribe(v => {
      this.fromDate.set(toIsoDate(v.start!));
      this.toDate.set(toIsoDate(v.end!));
      this.loadDashboard();
    });
  }

  loadDashboard(): void {
    this.loading.set(true);

    this.api.getSummary(this.fromDate(), this.toDate()).subscribe({
      next: response => {
        this.cards.set(response.cards);
        this.isStale.set(response.isStale);
        this.computedAt.set(response.computedAtUtc);
        this.hasData.set(response.cards.some(c => c.value > 0));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load KPI dashboard', 'Retry', {
          duration: 5000,
          panelClass: 'snackbar-error',
        });
      },
    });
  }

  /** Opens the distribution schedule configuration dialog (AC-4). */
  openDistributionConfig(): void {
    const ref = this.dialog.open(DistributionConfigDialogComponent, {
      width: '480px',
    });

    ref.afterClosed().subscribe(result => {
      if (result) {
        this.snackBar.open(
          `Distribution schedule saved (${result.recurrence})`,
          'Dismiss',
          { duration: 3000 },
        );
      }
    });
  }

  // ── Helpers ────────────────────────────────────────────────────────────────
  /** Skeleton array for loading state (4 cards). */
  readonly skeletonItems = [1, 2, 3, 4];

  private defaultFrom(): Date {
    const d = new Date();
    d.setDate(d.getDate() - 30);
    return d;
  }

  private defaultTo(): Date {
    return new Date();
  }
}

/** Formats a Date to YYYY-MM-DD ISO string for API query params. */
function toIsoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}
