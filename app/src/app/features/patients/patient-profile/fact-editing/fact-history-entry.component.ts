import {
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';
import { DatePipe } from '@angular/common';

import type { FactHistoryEntryDto } from '../../../../shared/models/clinical-fact.model';

/**
 * Renders a single fact audit history row (US_047 AC-3).
 *
 * Displays:
 * - Previous value (highlighted in amber)
 * - Editor display name
 * - Timestamp formatted as "MMM d, yyyy, h:mm a" in a semantic <time> element (UXR-201)
 */
@Component({
  selector: 'app-fact-history-entry',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    <div class="history-entry">
      <span class="history-entry__value" aria-label="Previous value">
        &ldquo;{{ entry().previousName ?? entry().previousValue }}&rdquo;
      </span>
      <span class="history-entry__meta">
        Edited by
        <span class="history-entry__editor">{{ entry().editorDisplayName }}</span>
        &ndash;
        <time
          class="history-entry__time"
          [dateTime]="entry().timestamp"
          [attr.title]="entry().timestamp | date:'medium'"
        >
          {{ entry().timestamp | date:'MMM d, yyyy, h:mm a' }}
        </time>
      </span>
    </div>
  `,
  styles: [`
    .history-entry {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 6px 8px;
      border-radius: 4px;
      background: #fafafa;
      border-left: 3px solid #f9a825;
    }

    .history-entry__value {
      font-size: 13px;
      font-weight: 600;
      color: #e65100;
      font-style: italic;
    }

    .history-entry__meta {
      font-size: 12px;
      color: var(--color-neutral-600, #757575);
    }

    .history-entry__editor {
      font-weight: 600;
      color: var(--color-neutral-800, #424242);
    }

    .history-entry__time {
      color: var(--color-neutral-500, #9e9e9e);
    }
  `],
})
export class FactHistoryEntryComponent {
  readonly entry = input.required<FactHistoryEntryDto>();
}
