import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

/**
 * Amber banner displayed when AI confidence is below the configured threshold (AC-3).
 *
 * Uses role="status" and aria-live="polite" so screen readers announce the
 * warning without interrupting the current reading flow (UXR-201).
 */
@Component({
  selector: 'app-low-confidence-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule],
  template: `
    <mat-card class="low-confidence-banner" appearance="outlined" role="status" aria-live="polite">
      <mat-card-content class="low-confidence-banner__content">
        <mat-icon aria-hidden="true" class="low-confidence-banner__icon">warning_amber</mat-icon>
        <span>
          <strong>Manual review recommended</strong> &mdash;
          AI confidence is below the minimum threshold.
        </span>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .low-confidence-banner {
      --mdc-outlined-card-container-color: #fff8e1;
      border-color: #ffb300;
    }

    .low-confidence-banner__content {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 4px 0;
    }

    .low-confidence-banner__icon {
      color: #f57f17;
      flex-shrink: 0;
    }

    span {
      font-size: 14px;
      color: var(--color-neutral-800, #424242);
    }
  `],
})
export class LowConfidenceBannerComponent {}
