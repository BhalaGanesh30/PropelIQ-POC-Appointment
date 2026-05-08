import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Skeleton loader for the clinical timeline (SCR-015 Loading state).
 * Shows 4 placeholder event cards matching the real card dimensions.
 */
@Component({
  selector: 'app-timeline-skeleton',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="skeleton-list" aria-label="Loading timeline events" aria-busy="true" role="status">
      @for (_ of items; track $index) {
        <div class="skeleton-card">
          <div class="skeleton-row skeleton-row--chip"></div>
          <div class="skeleton-row skeleton-row--title"></div>
          <div class="skeleton-row skeleton-row--body"></div>
        </div>
      }
    </div>
  `,
  styles: [`
    .skeleton-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .skeleton-card {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 14px 16px;
      border: 1px solid var(--color-neutral-200, #e0e0e0);
      border-radius: 8px;
      background: #fff;
    }

    .skeleton-row {
      border-radius: 4px;
      background: linear-gradient(
        90deg,
        var(--color-neutral-100, #f5f5f5) 25%,
        var(--color-neutral-200, #e0e0e0) 50%,
        var(--color-neutral-100, #f5f5f5) 75%
      );
      background-size: 400% 100%;
      animation: shimmer 1.4s infinite linear;
    }

    .skeleton-row--chip  { width: 90px; height: 20px; }
    .skeleton-row--title { width: 60%;  height: 16px; }
    .skeleton-row--body  { width: 90%;  height: 14px; }

    @keyframes shimmer {
      0%   { background-position: 100% 50%; }
      100% { background-position: 0%   50%; }
    }
  `],
})
export class TimelineSkeletonComponent {
  protected readonly items = Array(4);
}
