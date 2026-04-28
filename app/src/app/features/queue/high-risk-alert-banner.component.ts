import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { AppointmentRiskScore } from './models/risk-score.models';

/**
 * Alert banner for upcoming high-risk appointments (AC-3, US_028 task_003).
 *
 * Accessibility:
 *   UXR-203 — role="alert" + aria-live="assertive" so assistive technologies
 *   announce the banner immediately when the list is populated.
 *
 * Only rendered when the parent passes one or more High-risk appointments
 * scheduled within 24 hours.
 */
@Component({
  selector: 'app-high-risk-alert-banner',
  standalone: true,
  imports: [DatePipe, MatIconModule],
  template: `
    @if (appointments().length > 0) {
    <div
      role="alert"
      aria-live="assertive"
      aria-atomic="true"
      class="banner">
      <mat-icon class="banner-icon" aria-hidden="true">warning</mat-icon>
      <div class="banner-content">
        <strong>High-Risk Appointments in the Next 24 Hours</strong>
        <ul class="banner-list">
          @for (appt of appointments(); track appt.appointmentId) {
            <li>
              <span class="patient">{{ appt.patientName }}</span>
              &mdash;
              <span class="time">{{ appt.appointmentDate | date: 'h:mm a, MMM d' }}</span>
              <span class="type">({{ appt.appointmentType }})</span>
              <span class="confidence">
                {{ (appt.confidence * 100).toFixed(0) }}% confidence
              </span>
            </li>
          }
        </ul>
      </div>
    </div>
    }
  `,
  styles: [`
    :host { display: block; }

    .banner {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 12px 16px;
      background: #FFEBEE;
      border: 1px solid #EF9A9A;
      border-left: 4px solid #C62828;
      border-radius: 4px;
      margin-bottom: 16px;
    }

    .banner-icon {
      color: #C62828;
      flex-shrink: 0;
      margin-top: 2px;
    }

    .banner-content {
      flex: 1;
      font-size: 14px;
    }

    .banner-list {
      margin: 6px 0 0 0;
      padding-left: 16px;
      list-style: disc;
    }

    .banner-list li {
      margin-bottom: 2px;
    }

    .patient { font-weight: 600; }

    .type,
    .confidence {
      color: #616161;
      font-size: 12px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HighRiskAlertBannerComponent {
  /** High-risk appointments scheduled within the next 24 hours. */
  appointments = input.required<AppointmentRiskScore[]>();
}
