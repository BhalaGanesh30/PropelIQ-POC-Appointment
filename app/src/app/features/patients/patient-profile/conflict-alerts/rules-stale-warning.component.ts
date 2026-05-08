import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/**
 * Non-blocking amber warning banner shown when `rulesStale: true` is returned
 * by the conflict detection API (Edge Case 1 / UXR-404 amber).
 *
 * Does NOT block the display of alerts — informational only.
 */
@Component({
  selector: 'app-rules-stale-warning',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule],
  template: `
    @if (visible()) {
      <aside
        class="stale-warning"
        role="status"
        aria-live="polite"
        aria-label="Conflict detection rules may be outdated"
      >
        <mat-icon class="stale-warning__icon" aria-hidden="true">update</mat-icon>
        <div class="stale-warning__body">
          <p class="stale-warning__text">
            Conflict detection rules may be outdated. Results could be incomplete.
          </p>
        </div>
        <button
          mat-icon-button
          type="button"
          class="stale-warning__dismiss"
          aria-label="Dismiss outdated rules warning"
          (click)="visible.set(false)"
        >
          <mat-icon aria-hidden="true">close</mat-icon>
        </button>
      </aside>
    }
  `,
  styles: [`
    .stale-warning {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 14px;
      background: #fff8e1;
      border: 1px solid #ffe082;
      border-radius: 6px;
    }
    .stale-warning__icon { color: #f57f17; font-size: 20px; width: 20px; height: 20px; flex-shrink: 0; }
    .stale-warning__body { flex: 1; }
    .stale-warning__text { font-size: 13px; color: var(--color-neutral-800, #424242); margin: 0; }
    .stale-warning__dismiss { margin-left: auto; flex-shrink: 0; }
  `],
})
export class RulesStaleWarningComponent {
  protected readonly visible = signal(true);
}
