import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  OnInit,
  QueryList,
  ViewChildren,
  inject,
  input,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionPanel } from '@angular/material/expansion';

import { ClinicalTimelineFacade } from '../../clinical-timeline.facade';
import type { TimelineQueryParams } from '../../../../shared/models/timeline-event.model';
import { TimelineFilterBarComponent } from './timeline-filter-bar.component';
import { TimelineYearGroupComponent } from './timeline-year-group.component';
import { TimelineEmptyStateComponent } from './timeline-empty-state.component';
import { TimelineSkeletonComponent } from './timeline-skeleton.component';

/**
 * Clinical timeline tab content (SCR-015 / US_048).
 *
 * Orchestrates:
 * - Lazy load on init via `ClinicalTimelineFacade.load(patientId, {})`.
 * - Filter bar: debounced `filterChange` event → `facade.applyFilters()`.
 * - Year-grouped event rendering via `TimelineYearGroupComponent`.
 * - Loading skeleton, empty state, and error+retry banner (SCR-015 states).
 * - Print: expands all year panels before `window.print()`, restores state after `afterprint`.
 */
@Component({
  selector: 'app-clinical-timeline',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ClinicalTimelineFacade],
  imports: [
    MatButtonModule,
    MatIconModule,
    TimelineFilterBarComponent,
    TimelineYearGroupComponent,
    TimelineEmptyStateComponent,
    TimelineSkeletonComponent,
  ],
  template: `
    <div class="timeline-host">

      <!-- ── Toolbar row ────────────────────────────────────────── -->
      <div class="timeline-toolbar">
        <h2 class="timeline-toolbar__title" id="timeline-heading">Clinical Timeline</h2>
        <button
          mat-stroked-button
          type="button"
          class="print-btn"
          aria-label="Print timeline"
          (click)="print()"
        >
          <mat-icon aria-hidden="true">print</mat-icon>
          Print Timeline
        </button>
      </div>

      <!-- ── Filter bar ─────────────────────────────────────────── -->
      <app-timeline-filter-bar
        [initialFilters]="facade.activeFilters()"
        (filterChange)="onFilterChange($event)"
      />

      <!-- ── Content states ─────────────────────────────────────── -->
      <div class="timeline-content" aria-labelledby="timeline-heading">

        @if (facade.loading()) {
          <app-timeline-skeleton />

        } @else if (facade.error()) {
          <div class="error-banner" role="alert">
            <mat-icon aria-hidden="true">error_outline</mat-icon>
            <p>{{ facade.error() }}</p>
            <button mat-stroked-button type="button" (click)="facade.retry()">
              Retry
            </button>
          </div>

        } @else if (facade.events().length === 0) {
          <app-timeline-empty-state (uploadClicked)="onUploadCta()" />

        } @else {
          <!-- ── Year groups ────────────────────────────────────── -->
          @for (group of facade.groupedByYear(); track group.year) {
            <app-timeline-year-group
              [year]="group.year"
              [events]="group.events"
            />
          }
        }
      </div>

      <!-- ── Print header (hidden on screen, shown on print) ───── -->
      <div class="print-header" aria-hidden="true">
        <h1>Clinical Timeline</h1>
        <p class="print-header__meta">Patient ID: {{ patientId() }}</p>
        @if (facade.activeFilters().dateFrom || facade.activeFilters().dateTo) {
          <p class="print-header__range">
            Date range:
            {{ facade.activeFilters().dateFrom ?? 'All' }} –
            {{ facade.activeFilters().dateTo   ?? 'Present' }}
          </p>
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }

    .timeline-host {
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding: 8px 0;
    }

    .timeline-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }

    .timeline-toolbar__title {
      font-size: 18px;
      font-weight: 700;
      color: var(--color-neutral-800, #424242);
      margin: 0;
    }

    .print-btn mat-icon {
      margin-right: 4px;
    }

    .timeline-content {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .error-banner {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      padding: 32px;
      text-align: center;
      color: var(--color-neutral-700, #616161);

      mat-icon { font-size: 36px; width: 36px; height: 36px; color: #c62828; }
    }

    /* ── Print styles (AC-4) ───────────────────────────────────── */
    .print-header { display: none; }

    @media print {
      .print-header {
        display: block;
        margin-bottom: 16px;
        border-bottom: 2px solid #000;
        padding-bottom: 8px;

        h1 { font-size: 18px; margin: 0 0 4px; }
        p  { font-size: 12px; margin: 2px 0; }
      }

      /* Hide interactive chrome when printing */
      app-timeline-filter-bar,
      .timeline-toolbar,
      .print-btn { display: none !important; }

      /* Remove shadows and decorative borders for clean print */
      app-timeline-year-group,
      app-timeline-event-card { break-inside: avoid; }
    }

    @media (max-width: 768px) {
      .timeline-toolbar { flex-direction: column; align-items: flex-start; }
    }
  `],
})
export class ClinicalTimelineComponent implements OnInit, AfterViewInit {
  /** Patient GUID passed from the parent tab wrapper. */
  readonly patientId = input.required<string>();

  protected readonly facade = inject(ClinicalTimelineFacade);

  /** Used to expand all panels before printing (AC-4). */
  @ViewChildren(MatExpansionPanel)
  private readonly expansionPanels!: QueryList<MatExpansionPanel>;

  private _panelStateBeforePrint: boolean[] = [];
  private _afterPrintListener = () => this._restorePrintState();

  ngOnInit(): void {
    this.facade.load(this.patientId());
  }

  ngAfterViewInit(): void {
    // Register afterprint handler to restore panel expansion state after printing.
    window.addEventListener('afterprint', this._afterPrintListener);
  }

  // The component is destroyed with the tab — remove listener to avoid leaks.
  ngOnDestroy(): void {
    window.removeEventListener('afterprint', this._afterPrintListener);
  }

  protected onFilterChange(params: TimelineQueryParams): void {
    this.facade.applyFilters(params);
  }

  /** Navigate user to documents tab on empty-state upload CTA (Edge Case 1). */
  protected onUploadCta(): void {
    // Navigate is handled by the parent via router; here we dispatch a DOM event
    // the parent PatientProfileComponent can intercept if needed.
    window.dispatchEvent(new CustomEvent('navigate-to-documents'));
  }

  /** Expands all year panels before print then calls window.print() (AC-4). */
  protected print(): void {
    // Snapshot current expansion state.
    this._panelStateBeforePrint = this.expansionPanels.map((p) => p.expanded);
    // Expand all panels so printed output shows complete event list.
    this.expansionPanels.forEach((p) => p.open());
    window.print();
  }

  /** Restores panel expansion state after print dialog closes (AC-4). */
  private _restorePrintState(): void {
    this.expansionPanels.forEach((panel, i) => {
      if (this._panelStateBeforePrint[i]) {
        panel.open();
      } else {
        panel.close();
      }
    });
  }
}
