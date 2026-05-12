import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { ComplianceApiService } from './compliance-api.service';
import { ReportSchedule } from './models/compliance.models';

/**
 * Displays the list of configured compliance report schedules and allows
 * admins to toggle each schedule on/off (US_058, AC-1).
 *
 * Full schedule CRUD (create / edit recurrence, day, time) requires
 * server-side schedule endpoints not yet specified; those operations are
 * scaffolded as no-ops so the UI compiles and renders existing schedules.
 */
@Component({
  selector: 'app-schedule-config',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTableModule,
  ],
  templateUrl: './schedule-config.component.html',
})
export class ScheduleConfigComponent implements OnInit {
  private readonly api = inject(ComplianceApiService);

  readonly schedules  = signal<ReportSchedule[]>([]);
  readonly loading    = signal(false);
  readonly error      = signal(false);

  readonly displayedColumns: readonly string[] = [
    'name',
    'reportType',
    'recurrence',
    'nextRunAt',
    'lastRunAt',
    'active',
  ];

  ngOnInit(): void {
    this.loadSchedules();
  }

  loadSchedules(): void {
    this.loading.set(true);
    this.error.set(false);

    this.api.listSchedules().subscribe({
      next: (list) => {
        this.schedules.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }

  /** Toggles the active flag on a schedule (AC-1). */
  toggleActive(schedule: ReportSchedule): void {
    const newValue = !schedule.isActive;

    this.api.toggleSchedule(schedule.id, newValue).subscribe({
      next: (updated) => {
        this.schedules.update((list) =>
          list.map((s) => (s.id === updated.id ? updated : s)),
        );
      },
    });
  }
}
