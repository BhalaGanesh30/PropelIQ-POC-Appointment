import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import { ScrollingModule } from '@angular/cdk/scrolling';

import type { ClinicalFactDto } from '../../../../shared/models/clinical-fact.model';
import { ClinicalFactCardComponent } from './clinical-fact-card.component';
import { EmptyProfileStateComponent } from '../shared/empty-profile-state.component';

/** Items above this threshold use CDK virtual scroll (performance — AC-7). */
const VIRTUAL_SCROLL_THRESHOLD = 50;

/**
 * Renders a list of ClinicalFactDto items.
 * Uses CDK virtual scroll when count > VIRTUAL_SCROLL_THRESHOLD (AIR-004).
 * Falls back to simple @for loop for small lists.
 */
@Component({
  selector: 'app-fact-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ScrollingModule, ClinicalFactCardComponent, EmptyProfileStateComponent],
  template: `
    @if (facts().length === 0) {
      <app-empty-profile-state
        [message]="emptyMessage()"
        [icon]="emptyIcon()"
      />
    } @else if (useVirtualScroll()) {
      <!-- CDK virtual scroll when > 50 items -->
      <cdk-virtual-scroll-viewport
        class="fact-list-viewport"
        [itemSize]="96"
        [style.height]="viewportHeight()"
        aria-label="Clinical facts list"
      >
        <app-clinical-fact-card
          *cdkVirtualFor="let fact of facts(); trackBy: trackById"
          [fact]="fact"
          class="fact-list__item"
        />
      </cdk-virtual-scroll-viewport>
    } @else {
      <div class="fact-list" role="list" aria-label="Clinical facts list">
        @for (fact of facts(); track fact.factId) {
          <app-clinical-fact-card
            [fact]="fact"
            class="fact-list__item"
            (factUpdated)="factUpdated.emit($event)"
          />
        }
      </div>
    }
  `,
  styles: [`
    .fact-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .fact-list-viewport {
      width: 100%;
    }

    .fact-list__item {
      display: block;
    }
  `],
})
export class FactListComponent {
  readonly facts = input.required<ClinicalFactDto[]>();
  readonly emptyMessage = input<string>('No data available.');
  readonly emptyIcon = input<string>('info_outline');

  /** Bubbled up from ClinicalFactCardComponent after a successful edit or verify. */
  readonly factUpdated = output<ClinicalFactDto>();

  protected readonly useVirtualScroll = computed(
    () => this.facts().length > VIRTUAL_SCROLL_THRESHOLD,
  );

  protected readonly viewportHeight = computed(() => {
    const count = Math.min(this.facts().length, 8);
    return `${count * 96 + 16}px`;
  });

  protected trackById(_: number, fact: ClinicalFactDto): string {
    return fact.factId;
  }
}
