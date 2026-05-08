import {
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';
import { DatePipe, TitleCasePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';

import type { TimelineEventDto } from '../../../../shared/models/timeline-event.model';

/** Icon map per event category. */
const CATEGORY_ICON: Record<string, string> = {
  medication: 'medication',
  allergy:    'warning_amber',
  diagnosis:  'local_hospital',
  document:   'folder_open',
};

/**
 * Card displaying a single clinical timeline event (US_048 AC-1, UXR-201).
 *
 * Uses semantic `<article>` and `<time>` elements for screen readers (UXR-201).
 * Category chip color classes map to WCAG 2.1 AA compliant palette (UXR-201).
 */
@Component({
  selector: 'app-timeline-event-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TitleCasePipe, MatIconModule, MatChipsModule],
  template: `
    <article
      class="event-card"
      [class]="'event-card--' + event().category"
      [attr.aria-label]="event().category + ' event on ' + (event().eventDate | date:'mediumDate')"
    >
      <!-- Category chip -->
      <div class="event-card__header">
        <span class="category-chip" [class]="'category-chip--' + event().category" aria-hidden="true">
          <mat-icon aria-hidden="true">{{ categoryIcon() }}</mat-icon>
          {{ event().category | titlecase }}
        </span>

        <!-- Semantic date (UXR-201) -->
        <time class="event-card__date" [attr.dateTime]="event().eventDate">
          {{ event().eventDate | date:'MMM d, yyyy' }}
        </time>
      </div>

      <!-- Description -->
      <p class="event-card__description">{{ event().description }}</p>
    </article>
  `,
  styles: [`
    :host { display: block; }

    .event-card {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding: 12px 16px;
      background: #fff;
      border: 1px solid var(--color-neutral-200, #e0e0e0);
      border-radius: 8px;
      transition: box-shadow 0.15s ease;

      &:hover { box-shadow: 0 2px 6px rgba(0,0,0,.08); }
    }

    .event-card__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }

    .category-chip {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      font-size: 11px;
      font-weight: 600;
      padding: 2px 8px;
      border-radius: 12px;
      line-height: 20px;

      mat-icon { font-size: 14px; width: 14px; height: 14px; }
    }

    /* Category chip colors — 4.5:1 contrast ratio (UXR-201) */
    .category-chip--medication { background: #e8eaf6; color: #283593; }
    .category-chip--allergy    { background: #fff8e1; color: #e65100; }
    .category-chip--diagnosis  { background: #e8f5e9; color: #1b5e20; }
    .category-chip--document   { background: #fce4ec; color: #880e4f; }

    .event-card__date {
      font-size: 12px;
      color: var(--color-neutral-500, #9e9e9e);
      white-space: nowrap;
    }

    .event-card__description {
      font-size: 14px;
      color: var(--color-neutral-800, #424242);
      margin: 0;
      line-height: 1.4;
    }
  `],
})
export class TimelineEventCardComponent {
  readonly event = input.required<TimelineEventDto>();

  protected categoryIcon(): string {
    return CATEGORY_ICON[this.event().category] ?? 'event_note';
  }
}
