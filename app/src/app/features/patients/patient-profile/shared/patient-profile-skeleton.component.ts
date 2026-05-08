import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Skeleton loader shown while a tab's data is being fetched.
 * Uses CSS animated placeholders — no external dependency needed.
 */
@Component({
  selector: 'app-patient-profile-skeleton',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="skeleton-host" aria-label="Loading clinical data" aria-busy="true">
      @for (item of items; track $index) {
        <div class="skeleton-card">
          <div class="skeleton-line skeleton-line--title"></div>
          <div class="skeleton-line skeleton-line--body"></div>
          <div class="skeleton-line skeleton-line--body skeleton-line--short"></div>
        </div>
      }
    </div>
  `,
  styles: [`
    @keyframes shimmer {
      0%   { background-position: -800px 0; }
      100% { background-position: 800px 0; }
    }

    .skeleton-host {
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding: 8px 0;
    }

    .skeleton-card {
      background: #fff;
      border: 1px solid var(--color-neutral-200, #e0e0e0);
      border-radius: 8px;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .skeleton-line {
      border-radius: 4px;
      height: 14px;
      background: linear-gradient(90deg, #f5f5f5 25%, #ebebeb 50%, #f5f5f5 75%);
      background-size: 800px 100%;
      animation: shimmer 1.4s ease-in-out infinite;
    }

    .skeleton-line--title { width: 55%; height: 16px; }
    .skeleton-line--body  { width: 85%; }
    .skeleton-line--short { width: 40%; }
  `],
})
export class PatientProfileSkeletonComponent {
  protected readonly items = Array.from({ length: 5 });
}
