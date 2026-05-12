import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ComplianceApiService } from './compliance-api.service';
import { ComplianceReport } from './models/compliance.models';

/**
 * Paginated table of generated compliance reports (US_058, AC-2).
 *
 * SCR-022 states implemented:
 * - Default: table populated.
 * - Loading: indeterminate progress bar while fetching.
 * - Empty: "No reports generated yet" with CTA.
 * - Error: failure alert with retry button.
 * - Validation (Completed): download button and key-metrics column visible.
 */
@Component({
  selector: 'app-compliance-report-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  templateUrl: './compliance-report-list.component.html',
})
export class ComplianceReportListComponent implements OnInit {
  private readonly api = inject(ComplianceApiService);

  readonly reports    = signal<ComplianceReport[]>([]);
  readonly totalCount = signal(0);
  readonly loading    = signal(false);
  readonly error      = signal(false);

  readonly displayedColumns: readonly string[] = [
    'generatedAt',
    'period',
    'reportType',
    'status',
    'metrics',
    'actions',
  ];

  ngOnInit(): void {
    this.loadReports();
  }

  /** Public so parent container can call after new report is generated. */
  loadReports(page = 1): void {
    this.loading.set(true);
    this.error.set(false);

    this.api.list(page, 25).subscribe({
      next: (result) => {
        this.reports.set(result.items);
        this.totalCount.set(result.total);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }

  onPageChange(event: PageEvent): void {
    this.loadReports(event.pageIndex + 1);
  }

  /** Triggers a browser file-save for the PDF blob. */
  downloadPdf(report: ComplianceReport): void {
    this.api.downloadPdf(report.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `compliance-report-${report.id}.pdf`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
    });
  }

  /** Maps report status to a Material chip color. */
  statusColor(status: string): 'primary' | 'accent' | 'warn' | '' {
    switch (status) {
      case 'Completed':  return 'primary';
      case 'Generating': return 'accent';
      case 'Failed':     return 'warn';
      default:           return '';
    }
  }
}
