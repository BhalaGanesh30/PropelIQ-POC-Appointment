import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';

import { CodingDecisionFacade } from '../../../../../features/patients/coding-decision.facade';
import { PendingSubmissionBlockBannerComponent } from '../pending-submission-block-banner/pending-submission-block-banner.component';

/**
 * Summary bar for the SCR-017 Validation state (US_051 / AC-4).
 *
 * Displays:
 * - Running tally of pending / accepted / modified / rejected decisions from `CodingDecisionFacade`.
 * - `PendingSubmissionBlockBannerComponent` when any decisions are still pending (AC-4).
 *
 * Injected from the ancestor `CodingSuggestionPanelComponent` provider tree — reads the same
 * `CodingDecisionFacade` instance as the individual suggestion cards.
 *
 * Consumers that need to gate encounter submission should read `facade.allDecided()`:
 * ```html
 * <button [disabled]="!decisionFacade.allDecided()">Submit Encounter</button>
 * ```
 */
@Component({
  selector: 'app-coding-decision-summary-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatChipsModule, PendingSubmissionBlockBannerComponent],
  template: `
    <div class="summary-bar" aria-label="Coding decision summary">
      <div class="summary-bar__counts">
        <span class="count-item count-item--pending" [attr.aria-label]="facade.pendingCount() + ' pending'">
          <mat-icon aria-hidden="true" class="count-icon">pending_actions</mat-icon>
          <span class="count-value">{{ facade.pendingCount() }}</span>
          <span class="count-label">Pending</span>
        </span>

        <span class="count-item count-item--accepted" [attr.aria-label]="facade.acceptedCount() + ' accepted'">
          <mat-icon aria-hidden="true" class="count-icon">check_circle</mat-icon>
          <span class="count-value">{{ facade.acceptedCount() }}</span>
          <span class="count-label">Accepted</span>
        </span>

        <span class="count-item count-item--modified" [attr.aria-label]="facade.modifiedCount() + ' modified'">
          <mat-icon aria-hidden="true" class="count-icon">edit</mat-icon>
          <span class="count-value">{{ facade.modifiedCount() }}</span>
          <span class="count-label">Modified</span>
        </span>

        <span class="count-item count-item--rejected" [attr.aria-label]="facade.rejectedCount() + ' rejected'">
          <mat-icon aria-hidden="true" class="count-icon">cancel</mat-icon>
          <span class="count-value">{{ facade.rejectedCount() }}</span>
          <span class="count-label">Rejected</span>
        </span>
      </div>

      @if (facade.pendingCount() > 0) {
        <app-pending-submission-block-banner />
      }
    </div>
  `,
  styles: [`
    :host { display: block; }

    .summary-bar {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .summary-bar__counts {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      padding: 8px 12px;
      border-radius: 8px;
      background: var(--color-neutral-50, #fafafa);
      border: 1px solid var(--color-neutral-200, #eeeeee);
    }

    .count-item {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }

    .count-icon { font-size: 18px; width: 18px; height: 18px; }

    .count-value {
      font-size: 14px;
      font-weight: 700;
    }

    .count-label {
      font-size: 12px;
      color: var(--color-neutral-600, #757575);
    }

    .count-item--pending  .count-icon, .count-item--pending  .count-value  { color: #f57c00; }
    .count-item--accepted .count-icon, .count-item--accepted .count-value  { color: #2e7d32; }
    .count-item--modified .count-icon, .count-item--modified .count-value  { color: #e65100; }
    .count-item--rejected .count-icon, .count-item--rejected .count-value  { color: #616161; }
  `],
})
export class CodingDecisionSummaryBarComponent {
  protected readonly facade = inject(CodingDecisionFacade);
}
