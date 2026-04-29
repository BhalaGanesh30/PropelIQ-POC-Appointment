import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { AppointmentHistoryItem } from './models/appointment-history.models';

/**
 * Detail dialog for viewing appointment information (SCR-007).
 * Shown when the user clicks "View details" on a non-active appointment.
 */
@Component({
  selector: 'app-appointment-detail-dialog',
  standalone: true,
  imports: [
    DatePipe,
    MatDialogModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      <mat-icon aria-hidden="true">info</mat-icon>
      Appointment Details
    </h2>

    <mat-dialog-content>
      <dl class="detail-grid">
        <div class="detail-row">
          <dt>Date &amp; Time</dt>
          <dd>{{ data.scheduledAt | date : 'EEEE, MMM d, y h:mm a' }}</dd>
        </div>

        <div class="detail-row">
          <dt>Provider</dt>
          <dd>{{ data.providerName ?? 'TBD' }}</dd>
        </div>

        <div class="detail-row">
          <dt>Visit Type</dt>
          <dd>{{ data.appointmentType }}</dd>
        </div>

        <div class="detail-row">
          <dt>Duration</dt>
          <dd>{{ data.durationMinutes }} minutes</dd>
        </div>

        @if (data.location) {
          <div class="detail-row">
            <dt>Location</dt>
            <dd>{{ data.location }}</dd>
          </div>
        }

        <div class="detail-row">
          <dt>Confirmation Code</dt>
          <dd class="mono">{{ data.confirmationCode }}</dd>
        </div>

        <div class="detail-row">
          <dt>Status</dt>
          <dd>
            <mat-chip
              [class]="'status-chip status-' + data.status.toLowerCase()"
              disableRipple
            >
              {{ data.status === 'NoShow' ? 'No Show' : data.status }}
            </mat-chip>
          </dd>
        </div>
      </dl>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-flat-button mat-dialog-close aria-label="Close details dialog">
        Close
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      h2[mat-dialog-title] {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .detail-grid {
        display: grid;
        gap: 16px;
        margin: 0;
        padding: 0;
      }

      .detail-row {
        display: grid;
        grid-template-columns: 140px 1fr;
        gap: 8px;
        align-items: baseline;
      }

      dt {
        font-weight: 500;
        color: rgba(0, 0, 0, 0.6);
        font-size: 0.875rem;
      }

      dd {
        margin: 0;
        font-size: 0.95rem;
      }

      .mono {
        font-family: 'Roboto Mono', monospace;
        letter-spacing: 0.5px;
      }

      @media (max-width: 480px) {
        .detail-row {
          grid-template-columns: 1fr;
          gap: 2px;
        }
      }
    `,
  ],
})
export class AppointmentDetailDialogComponent {
  readonly data = inject<AppointmentHistoryItem>(MAT_DIALOG_DATA);
}
