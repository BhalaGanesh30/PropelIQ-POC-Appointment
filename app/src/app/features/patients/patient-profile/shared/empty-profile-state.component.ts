import {
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Empty state displayed when a tab has loaded successfully but has no data.
 * AC-3: Summary tab empty state message (UXR-107).
 */
@Component({
  selector: 'app-empty-profile-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="empty-state" role="status">
      <mat-icon class="empty-state__icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <p class="empty-state__message">{{ message() }}</p>
    </div>
  `,
  styles: [`
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 48px 24px;
      gap: 12px;
      text-align: center;
    }
    .empty-state__icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      color: var(--color-neutral-400, #bdbdbd);
    }
    .empty-state__message {
      font-size: 14px;
      color: var(--color-neutral-600, #757575);
      max-width: 320px;
      margin: 0;
    }
  `],
})
export class EmptyProfileStateComponent {
  readonly message = input<string>('No data available.');
  readonly icon = input<string>('info_outline');
}
