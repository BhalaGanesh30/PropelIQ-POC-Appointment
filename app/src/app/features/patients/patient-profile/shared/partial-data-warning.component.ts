import {
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

import type { PartialSourceDto } from '../../../../shared/models/clinical-fact.model';

/**
 * Banner shown when at least one data source returned a partial error (Edge Case 1).
 * Emits `retry` output that the parent can bind to `facade.reloadTab()`.
 */
@Component({
  selector: 'app-partial-data-warning',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatButtonModule],
  template: `
    <aside class="partial-warning" role="alert" aria-live="polite">
      <mat-icon class="partial-warning__icon" aria-hidden="true">warning</mat-icon>
      <div class="partial-warning__body">
        <span class="partial-warning__title">Partial data available</span>
        <p class="partial-warning__detail">
          The following sources are currently unavailable:
          @for (src of sources(); track src.sourceName) {
            <span class="partial-warning__source">{{ src.sourceName }}</span>
          }
        </p>
      </div>
    </aside>
  `,
  styles: [`
    .partial-warning {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 12px 16px;
      background: #fff8e1;
      border: 1px solid #ffe082;
      border-radius: 6px;
    }
    .partial-warning__icon { color: #f57f17; font-size: 20px; line-height: 24px; }
    .partial-warning__body { display: flex; flex-direction: column; gap: 2px; }
    .partial-warning__title { font-size: 13px; font-weight: 600; color: var(--color-neutral-900); }
    .partial-warning__detail { font-size: 12px; color: var(--color-neutral-700); margin: 2px 0 0; }
    .partial-warning__source {
      display: inline-block; background: #ffe082; border-radius: 4px;
      padding: 1px 6px; margin: 2px 4px 0 0; font-size: 12px; font-weight: 500;
    }
  `],
})
export class PartialDataWarningComponent {
  readonly sources = input.required<PartialSourceDto[]>();
}
