import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Informational note rendered below suggestion cards when fewer than 3
 * ICD-10 codes are returned (Edge Case 1).
 */
@Component({
  selector: 'app-insufficient-evidence-note',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="insufficient-note" role="note">
      <mat-icon aria-hidden="true">info_outline</mat-icon>
      <span>Insufficient evidence for a third suggestion.</span>
    </div>
  `,
  styles: [`
    .insufficient-note {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px;
      border-radius: 8px;
      background: var(--color-neutral-100, #f5f5f5);
      color: var(--color-neutral-600, #757575);
      font-size: 13px;

      mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
        flex-shrink: 0;
      }
    }
  `],
})
export class InsufficientEvidenceNoteComponent {}
