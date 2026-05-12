import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

/**
 * Amber warning banner shown when the CPT code database is stale (>90 days).
 * Rendered above the CPT suggestion section when the API returns
 * `staleDatabaseWarning: true` (US_050 Edge Case 2).
 *
 * role="status" + aria-live="polite" ensures assistive technologies announce
 * the warning without interrupting ongoing interaction.
 */
@Component({
  selector: 'app-stale-cpt-database-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule],
  template: `
    <mat-card
      class="stale-banner"
      appearance="outlined"
      role="status"
      aria-live="polite"
      aria-atomic="true"
    >
      <mat-card-content class="stale-banner__content">
        <mat-icon aria-hidden="true" class="stale-banner__icon">warning_amber</mat-icon>
        <p class="stale-banner__message">
          CPT code database may be outdated &mdash; suggestions may include deprecated codes.
          Contact your administrator.
        </p>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    :host { display: block; }

    .stale-banner {
      background: #fff8e1;
      border-color: #ffb300;
    }

    .stale-banner__content {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 16px;
    }

    .stale-banner__icon {
      color: #f57f17;
      flex-shrink: 0;
    }

    .stale-banner__message {
      font-size: 13px;
      color: #5d4037;
      margin: 0;
      line-height: 1.5;
    }
  `],
})
export class StaleCptDatabaseBannerComponent {}
