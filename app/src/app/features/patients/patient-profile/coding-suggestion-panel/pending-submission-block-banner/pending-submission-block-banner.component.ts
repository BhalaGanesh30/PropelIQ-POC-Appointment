import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * "Coding decisions required" blocking banner (US_051 / AC-4).
 *
 * Rendered by `CodingDecisionSummaryBarComponent` when `pendingCount() > 0`.
 * Blocks encounter submission by informing the clinician which codes are still pending.
 *
 * ARIA: `role="alert"` + `aria-live="assertive"` so screen readers announce it immediately
 * when it appears (e.g. on an attempted submit with outstanding pending decisions).
 */
@Component({
  selector: 'app-pending-submission-block-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div
      role="alert"
      aria-live="assertive"
      class="block-banner"
    >
      <mat-icon aria-hidden="true" class="block-banner__icon">block</mat-icon>
      <div class="block-banner__body">
        <p class="block-banner__title">Coding decisions required</p>
        <p class="block-banner__subtitle">
          All suggestions must be accepted, modified, or rejected before the encounter can be submitted.
        </p>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }

    .block-banner {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 12px 16px;
      border-radius: 8px;
      background: #fce4ec;
      border: 1px solid #e91e63;
    }

    .block-banner__icon {
      color: #c62828;
      flex-shrink: 0;
      margin-top: 2px;
    }

    .block-banner__body {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .block-banner__title {
      font-size: 14px;
      font-weight: 600;
      color: #b71c1c;
      margin: 0;
    }

    .block-banner__subtitle {
      font-size: 13px;
      color: #c62828;
      margin: 0;
    }
  `],
})
export class PendingSubmissionBlockBannerComponent {}
