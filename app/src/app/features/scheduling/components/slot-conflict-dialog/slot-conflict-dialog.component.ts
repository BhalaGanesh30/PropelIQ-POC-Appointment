import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { SlotConflictResponse } from '../../models/booking.model';

/**
 * Material dialog shown when POST /api/v1/bookings returns HTTP 409.
 * AC-4: displays "Slot no longer available" and the next available time.
 * Closed with:
 *   - `undefined`           → user dismissed (Cancel)
 *   - `'search'`            → navigate to slot search
 *   - `<slotId string>`     → rebook with suggested slot
 */
@Component({
  selector: 'app-slot-conflict-dialog',
  standalone: true,
  imports: [DatePipe, MatButtonModule, MatDialogModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      <mat-icon color="warn">event_busy</mat-icon>
      Slot No Longer Available
    </h2>

    <mat-dialog-content>
      <p>{{ data.message }}</p>
      @if (data.nextAvailableTime) {
        <p class="next-slot">
          Next available slot:
          <strong>{{ data.nextAvailableTime | date: 'EEEE, MMM d, y h:mm a' }}</strong>
        </p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close aria-label="Cancel and dismiss">
        Cancel
      </button>
      <button mat-stroked-button
              (click)="dialogRef.close('search')"
              aria-label="Go back to slot search">
        Search Again
      </button>
      @if (data.nextAvailableSlotId) {
        <button mat-flat-button color="primary"
                [mat-dialog-close]="data.nextAvailableSlotId"
                aria-label="Book the next available slot">
          Book Next Slot
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: [`
    h2 { display: flex; align-items: center; gap: 8px; }
    .next-slot {
      margin-top: 16px;
      padding: 12px;
      background: var(--mat-sys-surface-variant);
      border-radius: 8px;
    }
  `],
})
export class SlotConflictDialogComponent {
  readonly data = inject<SlotConflictResponse>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<SlotConflictDialogComponent>);
}
