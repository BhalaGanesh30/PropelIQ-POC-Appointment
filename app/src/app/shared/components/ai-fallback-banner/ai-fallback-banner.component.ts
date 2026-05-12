import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

/**
 * Global amber fallback banner shown on all AI-dependent screens (SCR-014, SCR-015, SCR-017)
 * while the AI gateway circuit breaker is open (US_053, AC-2, Edge Case 2).
 *
 * The banner is non-dismissable — it disappears automatically when the circuit closes
 * (i.e. when `AiGatewayStatusFacade.fallbackActive()` returns `false`) without a page reload.
 *
 * Accessibility:
 *   - `role="status"` + `aria-live="polite"`: screen readers announce the fallback state
 *     without interrupting the current reading flow (Web Accessibility Standards, WA-001).
 *   - No interactive controls — the banner is purely informational.
 *
 * Styling mirrors `LowConfidenceBannerComponent` — same amber `mat-card` pattern used for
 * consistent warning-state visual language across the application.
 */
@Component({
  selector: 'app-ai-fallback-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule],
  template: `
    <mat-card
      class="ai-fallback-banner"
      appearance="outlined"
      role="status"
      aria-live="polite"
      aria-label="AI assistance temporarily unavailable"
    >
      <mat-card-content class="ai-fallback-banner__content">
        <mat-icon aria-hidden="true" class="ai-fallback-banner__icon">warning_amber</mat-icon>
        <span>
          <strong>AI assistance temporarily unavailable.</strong>
          Manual coding mode is active.
        </span>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .ai-fallback-banner {
      --mdc-outlined-card-container-color: #fff8e1;
      border-color: #ffb300;
      margin-bottom: 16px;
    }

    .ai-fallback-banner__content {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 4px 0;
    }

    .ai-fallback-banner__icon {
      color: #f57f17;
      flex-shrink: 0;
    }

    span {
      font-size: 14px;
      color: var(--color-neutral-800, #424242);
    }
  `],
})
export class AiFallbackBannerComponent {}
