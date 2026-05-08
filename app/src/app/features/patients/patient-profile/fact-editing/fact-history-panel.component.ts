import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  signal,
} from '@angular/core';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { ClinicalFactService } from '../../clinical-fact.service';
import type { FactHistoryEntryDto } from '../../../../shared/models/clinical-fact.model';
import { FactHistoryEntryComponent } from './fact-history-entry.component';

/**
 * Lazy-loading audit history expansion panel for a clinical fact (US_047 AC-3).
 *
 * Opens on user interaction — calls `GET /api/v1/clinical-facts/{id}/history`
 * only once, on the first panel open. Subsequent opens use the cached Signal.
 * Shows a skeleton spinner while loading, "No edit history" when empty.
 */
@Component({
  selector: 'app-fact-history-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatExpansionModule, MatProgressSpinnerModule, FactHistoryEntryComponent],
  template: `
    <mat-expansion-panel
      class="history-panel"
      [attr.aria-label]="'Edit history for ' + factName()"
      (opened)="onOpened()"
    >
      <mat-expansion-panel-header>
        <mat-panel-title class="history-panel__title">History</mat-panel-title>
      </mat-expansion-panel-header>

      @if (loading()) {
        <div class="history-panel__loading" aria-busy="true" aria-label="Loading history">
          <mat-spinner diameter="24" strokeWidth="3" />
        </div>
      } @else if (entries().length === 0) {
        <p class="history-panel__empty">No edit history.</p>
      } @else {
        <div class="history-panel__list" role="list">
          @for (entry of entries(); track entry.auditId) {
            <app-fact-history-entry [entry]="entry" role="listitem" />
          }
        </div>
      }
    </mat-expansion-panel>
  `,
  styles: [`
    .history-panel {
      margin-top: 8px;
      box-shadow: none !important;
      border: 1px solid var(--color-neutral-200, #e0e0e0) !important;
      border-radius: 6px !important;
    }

    .history-panel__title {
      font-size: 13px;
      font-weight: 600;
      color: var(--color-neutral-700, #616161);
    }

    .history-panel__loading {
      display: flex;
      justify-content: center;
      padding: 12px 0;
    }

    .history-panel__empty {
      font-size: 13px;
      color: var(--color-neutral-500, #9e9e9e);
      margin: 4px 0 8px;
      text-align: center;
    }

    .history-panel__list {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding-bottom: 4px;
    }
  `],
})
export class FactHistoryPanelComponent {
  readonly factId = input.required<string>();
  readonly factName = input<string>('');

  private readonly factService = inject(ClinicalFactService);

  readonly loading = signal(false);
  readonly entries = signal<FactHistoryEntryDto[]>([]);
  private _loaded = false;

  onOpened(): void {
    if (this._loaded) return;
    this._loaded = true;
    this.loading.set(true);

    this.factService.getFactHistory(this.factId()).subscribe({
      next: (history) => {
        this.entries.set(history);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }
}
