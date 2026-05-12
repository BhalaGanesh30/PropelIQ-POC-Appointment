import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Color, NgxChartsModule, ScaleType } from '@swimlane/ngx-charts';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { KpiApiService } from './kpi-api.service';
import {
  KPI_METRIC_CONFIG,
  KpiCardValue,
  KpiExportFormat,
  KpiMetricConfig,
} from './models/kpi.models';

/**
 * Individual KPI metric card with headline value, trend indicator,
 * ngx-charts line chart, and per-chart export (US_060, AC-1–AC-3).
 *
 * States:
 * - Loading: spinner overlay on chart area.
 * - Error:   inline retry button — partial dashboard renders normally.
 * - Empty:   "No data for the selected period" annotation (edge case 2).
 * - Default: ngx-charts line chart with time-series data.
 *
 * Reacts to `from`/`to` signal changes via `effect()` to reload the chart
 * within 1 second (AC-2).
 */
@Component({
  selector: 'app-kpi-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    NgxChartsModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './kpi-card.component.html',
  styleUrl: './kpi-card.component.scss',
})
export class KpiCardComponent {
  // ── Inputs ────────────────────────────────────────────────────────────────
  readonly card = input.required<KpiCardValue>();
  readonly from = input.required<string>();
  readonly to   = input.required<string>();

  private readonly api = inject(KpiApiService);

  // ── Derived display config ─────────────────────────────────────────────────
  readonly config = computed<KpiMetricConfig | undefined>(
    () => KPI_METRIC_CONFIG.find(c => c.key === this.card().metric),
  );

  /** ngx-charts color scheme — domain contains the per-metric semantic color. */
  readonly colorScheme = computed<Color>(() => ({
    name: this.card().metric,
    selectable: false,
    group: ScaleType.Ordinal,
    domain: [this.config()?.color ?? '#1E88E5'],
  }));

  // ── Chart state ────────────────────────────────────────────────────────────
  // ngx-charts line chart data format: [{ name: string, series: [{name, value}] }]
  readonly chartData = signal<{ name: string; series: { name: string; value: number }[] }[]>([]);
  readonly chartLoading = signal(false);
  readonly chartError   = signal(false);
  readonly isEmpty      = signal(false);

  constructor() {
    // React to date-range input changes — reload chart whenever from/to updates.
    effect(() => {
      const from = this.from();
      const to   = this.to();
      if (from && to) {
        this.loadChart(from, to);
      }
    });
  }

  /** Loads (or re-loads) the time-series data for this metric. */
  loadChart(from = this.from(), to = this.to()): void {
    this.chartLoading.set(true);
    this.chartError.set(false);

    this.api.getTimeSeries(this.card().metric, from, to).subscribe({
      next: response => {
        const isEmpty = response.points.length === 0;
        this.isEmpty.set(isEmpty);

        if (!isEmpty) {
          this.chartData.set([{
            name: this.config()?.label ?? this.card().metric,
            series: response.points.map(p => ({ name: p.date, value: p.value })),
          }]);
        }

        this.chartLoading.set(false);
      },
      error: () => {
        this.chartError.set(true);
        this.chartLoading.set(false);
      },
    });
  }

  /** Downloads the KPI export as PNG or PDF (AC-3). */
  exportChart(format: KpiExportFormat): void {
    this.api.export(this.from(), this.to(), format).subscribe({
      next: blob => {
        const ext = format === 'Png' ? 'png' : 'pdf';
        const url = URL.createObjectURL(blob);
        const a   = document.createElement('a');
        a.href     = url;
        a.download = `kpi-${this.card().metric}-${this.from()}-${this.to()}.${ext}`;
        a.click();
        URL.revokeObjectURL(url);
      },
    });
  }

  /** True when change is positive (for aria label). */
  changeIsPositive(change: number | null): boolean {
    return change !== null && change >= 0;
  }
}
