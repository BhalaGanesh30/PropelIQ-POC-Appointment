import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

/**
 * Empty state for the clinical timeline (US_048 Edge Case 1, SCR-015 Empty state).
 *
 * Shown when `events().length === 0` and the view is not loading.
 * Provides a document upload CTA so clinicians can populate the timeline.
 */
@Component({
  selector: 'app-timeline-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatButtonModule],
  template: `
    <div class="empty-state" role="status" aria-live="polite">
      <mat-icon class="empty-state__icon" aria-hidden="true">timeline</mat-icon>
      <h3 class="empty-state__title">No clinical events recorded yet.</h3>
      <p class="empty-state__subtitle">
        Upload clinical documents to generate the patient timeline automatically.
      </p>
      <button
        mat-stroked-button
        type="button"
        class="empty-state__cta"
        (click)="uploadClicked.emit()"
        aria-label="Go to documents to upload clinical files"
      >
        <mat-icon aria-hidden="true">upload_file</mat-icon>
        Upload Documents
      </button>
    </div>
  `,
  styles: [`
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      padding: 48px 24px;
      text-align: center;
    }

    .empty-state__icon {
      font-size: 56px;
      width: 56px;
      height: 56px;
      color: var(--color-neutral-300, #bdbdbd);
    }

    .empty-state__title {
      font-size: 16px;
      font-weight: 600;
      color: var(--color-neutral-700, #616161);
      margin: 0;
    }

    .empty-state__subtitle {
      font-size: 14px;
      color: var(--color-neutral-500, #9e9e9e);
      margin: 0;
      max-width: 360px;
    }

    .empty-state__cta {
      margin-top: 8px;
    }
  `],
})
export class TimelineEmptyStateComponent {
  /** Emitted when the user clicks "Upload Documents" CTA. */
  readonly uploadClicked = output<void>();
}
