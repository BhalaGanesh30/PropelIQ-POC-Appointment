import {
  ChangeDetectionStrategy,
  Component,
  input,
  computed,
} from '@angular/core';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatBadgeModule } from '@angular/material/badge';
import { MatIconModule } from '@angular/material/icon';

import type { TimelineEventDto } from '../../../../shared/models/timeline-event.model';
import { TimelineEventCardComponent } from './timeline-event-card.component';

/**
 * Expansion panel grouping timeline events by calendar year (US_048 Edge Case 2).
 *
 * Current year is expanded by default; all prior years are collapsed (AC-1, Edge Case 2).
 * Each event renders a `TimelineEventCardComponent` connected by a CSS vertical line.
 * The `[expanded]` binding is controlled by the print mode as well (AC-4).
 */
@Component({
  selector: 'app-timeline-year-group',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatExpansionModule, MatBadgeModule, MatIconModule, TimelineEventCardComponent],
  template: `
    <mat-expansion-panel
      [expanded]="isCurrentYear()"
      class="year-panel"
      hideToggle
    >
      <mat-expansion-panel-header class="year-panel__header">
        <mat-panel-title class="year-panel__title">
          <mat-icon aria-hidden="true">calendar_today</mat-icon>
          <span>{{ year() }}</span>
          <span class="year-panel__badge" [attr.aria-label]="events().length + ' events'">
            {{ events().length }}
          </span>
        </mat-panel-title>
        <mat-panel-description class="year-panel__desc" aria-hidden="true">
          {{ isCurrentYear() ? 'Current year' : '' }}
        </mat-panel-description>
      </mat-expansion-panel-header>

      <!-- Timeline entry list with vertical connector line -->
      <div class="timeline-entries" role="list">
        @for (event of events(); track event.eventId) {
          <div class="timeline-entry" role="listitem">
            <div class="timeline-entry__dot" aria-hidden="true"></div>
            <app-timeline-event-card [event]="event" class="timeline-entry__card" />
          </div>
        }
      </div>
    </mat-expansion-panel>
  `,
  styles: [`
    :host { display: block; }

    .year-panel {
      box-shadow: none !important;
      border: 1px solid var(--color-neutral-200, #e0e0e0);
      border-radius: 8px !important;
      margin-bottom: 0 !important;
    }

    .year-panel__header {
      background: var(--color-neutral-50, #fafafa);
      border-radius: 8px;
    }

    .year-panel__title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 15px;
      font-weight: 700;
      color: var(--color-neutral-800, #424242);

      mat-icon { color: var(--color-neutral-500, #9e9e9e); }
    }

    .year-panel__badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 22px;
      height: 22px;
      padding: 0 6px;
      background: #1976d2;
      color: #fff;
      border-radius: 11px;
      font-size: 11px;
      font-weight: 700;
    }

    .year-panel__desc {
      font-size: 12px;
      color: var(--color-neutral-400, #bdbdbd);
    }

    /* Vertical timeline connector line (Edge Case 2) */
    .timeline-entries {
      position: relative;
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding: 12px 0 4px 28px;

      &::before {
        content: '';
        position: absolute;
        left: 12px;
        top: 20px;
        bottom: 16px;
        width: 2px;
        background: var(--color-neutral-200, #e0e0e0);
      }
    }

    .timeline-entry {
      position: relative;
    }

    .timeline-entry__dot {
      position: absolute;
      left: -22px;
      top: 14px;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: #1976d2;
      border: 2px solid #fff;
      box-shadow: 0 0 0 2px #1976d2;
    }

    .timeline-entry__card {
      display: block;
    }

    @media print {
      .year-panel { border: none; border-bottom: 1px solid #ccc; border-radius: 0 !important; }
    }
  `],
})
export class TimelineYearGroupComponent {
  readonly year = input.required<number>();
  readonly events = input.required<TimelineEventDto[]>();

  protected readonly isCurrentYear = computed(
    () => this.year() === new Date().getFullYear(),
  );
}
