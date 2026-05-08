import {
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Inline error banner displayed when a `PATCH` returns HTTP 409 (optimistic concurrency
 * conflict — Edge Case 1).
 *
 * Shows the current server value and instructs the user to refresh and retry.
 */
@Component({
  selector: 'app-concurrency-conflict-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="conflict-banner" role="alert" aria-live="assertive">
      <mat-icon aria-hidden="true" class="conflict-banner__icon">warning</mat-icon>
      <div class="conflict-banner__body">
        <strong class="conflict-banner__title">Edit conflict</strong>
        <span class="conflict-banner__msg">
          Another user changed this fact. Current value:
          <em class="conflict-banner__value">"{{ currentValue() }}"</em>
        </span>
        <span class="conflict-banner__hint">Refresh the page and retry with the latest data.</span>
      </div>
    </div>
  `,
  styles: [`
    .conflict-banner {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      padding: 10px 12px;
      background: #fce4ec;
      border: 1px solid #e91e63;
      border-radius: 6px;
      margin-bottom: 8px;
    }

    .conflict-banner__icon {
      color: #c62828;
      flex-shrink: 0;
      margin-top: 2px;
    }

    .conflict-banner__body {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .conflict-banner__title {
      font-size: 13px;
      font-weight: 700;
      color: #b71c1c;
    }

    .conflict-banner__msg,
    .conflict-banner__hint {
      font-size: 13px;
      color: #c62828;
    }

    .conflict-banner__value {
      font-style: italic;
      font-weight: 600;
    }
  `],
})
export class ConcurrencyConflictBannerComponent {
  readonly currentValue = input.required<string>();
}
