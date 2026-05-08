import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Skeleton loader matching the alert card layout (SCR-016 Loading state).
 * Shows shimmer animation while conflict data is being fetched.
 */
@Component({
  selector: 'app-conflict-alerts-skeleton',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="skeleton-host" aria-label="Loading conflict alerts" aria-busy="true">
      @for (item of items; track $index) {
        <div class="skeleton-card">
          <div class="skeleton-row">
            <div class="skeleton-badge"></div>
            <div class="skeleton-badge skeleton-badge--type"></div>
          </div>
          <div class="skeleton-line skeleton-line--desc"></div>
          <div class="skeleton-pair"></div>
        </div>
      }
    </div>
  `,
  styles: [`
    @keyframes shimmer {
      0%   { background-position: -800px 0; }
      100% { background-position: 800px 0; }
    }

    .skeleton-host { display: flex; flex-direction: column; gap: 12px; }

    .skeleton-card {
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 14px 16px;
      background: #fff;
      border: 1px solid var(--color-neutral-200, #e0e0e0);
      border-left: 4px solid #e0e0e0;
      border-radius: 8px;
    }

    .skeleton-row { display: flex; gap: 8px; align-items: center; }

    .skeleton-badge {
      width: 72px;
      height: 22px;
      border-radius: 12px;
      background: linear-gradient(90deg, #f5f5f5 25%, #ebebeb 50%, #f5f5f5 75%);
      background-size: 800px 100%;
      animation: shimmer 1.4s ease-in-out infinite;
      border-radius: 4px;
    }

    .skeleton-badge--type { width: 90px; }

    .skeleton-line {
      height: 14px;
      background: linear-gradient(90deg, #f5f5f5 25%, #ebebeb 50%, #f5f5f5 75%);
      background-size: 800px 100%;
      animation: shimmer 1.4s ease-in-out infinite;
      border-radius: 4px;
    }

    .skeleton-line--desc { width: 85%; }

    .skeleton-pair {
      height: 36px;
      width: 55%;
      border-radius: 6px;
      background: linear-gradient(90deg, #f5f5f5 25%, #ebebeb 50%, #f5f5f5 75%);
      background-size: 800px 100%;
      animation: shimmer 1.4s ease-in-out infinite;
    }
  `],
})
export class ConflictAlertsSkeletonComponent {
  protected readonly items = Array.from({ length: 3 });
}
