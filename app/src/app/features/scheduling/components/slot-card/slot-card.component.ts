import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { SlotDto } from '../../models/slot.model';

/**
 * Individual appointment slot card.
 *
 * UXR-202: keyboard-navigable via tabindex + Enter/Space handler.
 * UXR-304: min-height 44px touch target.
 * UXR-503: selected state renders border emphasis.
 */
@Component({
  selector: 'app-slot-card',
  standalone: true,
  imports: [MatCardModule, MatIconModule, DatePipe],
  templateUrl: './slot-card.component.html',
  styleUrl: './slot-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SlotCardComponent {
  readonly slot = input.required<SlotDto>();
  readonly isSelected = input(false);
  readonly slotSelected = output<SlotDto>();

  onSelect(): void {
    this.slotSelected.emit(this.slot());
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.onSelect();
    }
  }
}
