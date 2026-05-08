import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * "No conflicts detected" success state (SCR-016 Empty state).
 * Displayed when the alerts array is empty and data has loaded.
 */
@Component({
  selector: 'app-conflict-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="empty-state" role="status" aria-live="polite">
      <mat-icon class="empty-state__icon" aria-hidden="true">check_circle</mat-icon>
      <p class="empty-state__title">No conflicts detected</p>
      <p class="empty-state__subtitle">
        No drug–drug or drug–allergy interactions found for this patient's current medications.
      </p>
    </div>
  `,
  styles: [`
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 48px 24px;
      text-align: center;
    }
    .empty-state__icon {
      font-size: 48px; width: 48px; height: 48px; color: #2e7d32;
    }
    .empty-state__title {
      font-size: 16px; font-weight: 600;
      color: var(--color-neutral-900, #212121); margin: 0;
    }
    .empty-state__subtitle {
      font-size: 14px; color: var(--color-neutral-600, #757575);
      max-width: 320px; margin: 0;
    }
  `],
})
export class ConflictEmptyStateComponent {}
