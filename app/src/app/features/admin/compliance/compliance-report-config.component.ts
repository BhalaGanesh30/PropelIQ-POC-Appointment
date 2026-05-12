import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  inject,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Subscription, interval } from 'rxjs';
import { switchMap, takeWhile } from 'rxjs/operators';
import { ComplianceApiService } from './compliance-api.service';
import { GenerateReportRequest } from './models/compliance.models';

/**
 * Configuration form for on-demand compliance report generation (US_058, AC-4).
 *
 * States:
 * - Idle: form enabled, Generate button visible.
 * - Generating (sync): indeterminate progress bar until complete.
 * - Generating (async > 2 min): determinate progress bar via 5-s polling;
 *   email-notification message shown (edge case 1).
 * - Error: form re-enabled; parent catches via (generateError) output.
 *
 * Emits `reportGenerated` when the report (sync or async) is ready so the
 * parent page can refresh the report list.
 */
@Component({
  selector: 'app-compliance-report-config',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
  ],
  templateUrl: './compliance-report-config.component.html',
})
export class ComplianceReportConfigComponent implements OnDestroy {
  /** Fires when a report finishes generating (sync or async). */
  readonly reportGenerated = output<void>();

  private readonly api = inject(ComplianceApiService);

  // ── Form state ──────────────────────────────────────────────────────────────
  readonly reportType   = signal<string>('HIPAA');
  readonly startDate    = signal<Date | null>(null);
  readonly endDate      = signal<Date | null>(null);

  // ── Generation state ────────────────────────────────────────────────────────
  readonly generating   = signal(false);
  readonly isAsync      = signal(false);
  readonly asyncJobId   = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  private pollSub: Subscription | null = null;

  // ── Actions ─────────────────────────────────────────────────────────────────

  /** Validates inputs and dispatches the generate request. */
  generate(): void {
    const start = this.startDate();
    const end   = this.endDate();
    if (!start || !end) return;

    this.errorMessage.set(null);
    this.generating.set(true);
    this.isAsync.set(false);

    const req: GenerateReportRequest = {
      reportType:     this.reportType(),
      periodStartUtc: start.toISOString(),
      periodEndUtc:   end.toISOString(),
    };

    this.api.generate(req).subscribe({
      next: (res) => {
        if (res.isAsync && res.jobId) {
          // Large date range — poll status endpoint every 5 s (edge case 1).
          this.isAsync.set(true);
          this.asyncJobId.set(res.jobId);
          this.startPolling(res.jobId);
        } else {
          this.finishGenerating();
        }
      },
      error: (err) => {
        const msg = err?.error?.title ?? 'Report generation failed. Please try again.';
        this.errorMessage.set(msg);
        this.generating.set(false);
      },
    });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  // ── Private helpers ─────────────────────────────────────────────────────────

  private startPolling(jobId: string): void {
    this.pollSub?.unsubscribe();

    this.pollSub = interval(5_000)
      .pipe(
        switchMap(() => this.api.getJobStatus(jobId)),
        takeWhile(
          (status) => status.status !== 'Completed' && status.status !== 'Failed',
          /* inclusive = */ true,
        ),
      )
      .subscribe({
        next: (status) => {
          if (status.status === 'Completed' || status.status === 'Failed') {
            this.asyncJobId.set(null);
            this.finishGenerating();
          }
        },
        error: () => {
          // Network error during poll — stop gracefully.
          this.asyncJobId.set(null);
          this.finishGenerating();
        },
      });
  }

  private finishGenerating(): void {
    this.generating.set(false);
    this.isAsync.set(false);
    this.reportGenerated.emit();
  }
}
