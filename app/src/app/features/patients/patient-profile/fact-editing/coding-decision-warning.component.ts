import {
  ChangeDetectionStrategy,
  Component,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Non-blocking amber warning shown when a fact being edited is referenced by a
 * coding decision (Edge Case 2 — US_047).
 *
 * The user is informed that the coding decision should be reviewed after saving,
 * but the save is not blocked.
 */
@Component({
  selector: 'app-coding-decision-warning',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="coding-warning" role="status" aria-live="polite">
      <mat-icon aria-hidden="true" class="coding-warning__icon">info</mat-icon>
      <p class="coding-warning__msg">
        This fact is referenced by a coding decision.
        Review the coding decision after saving.
      </p>
    </div>
  `,
  styles: [`
    .coding-warning {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      background: #fff8e1;
      border: 1px solid #f9a825;
      border-radius: 6px;
      margin-bottom: 8px;
    }

    .coding-warning__icon {
      color: #f57f17;
      flex-shrink: 0;
    }

    .coding-warning__msg {
      margin: 0;
      font-size: 13px;
      color: #e65100;
      line-height: 1.4;
    }
  `],
})
export class CodingDecisionWarningComponent {}
