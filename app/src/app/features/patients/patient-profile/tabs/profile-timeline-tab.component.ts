import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ClinicalTimelineComponent } from '../clinical-timeline/clinical-timeline.component';

/**
 * Timeline tab wrapper for the 360° patient profile (SCR-015 / US_048).
 * Passes the patient ID to `ClinicalTimelineComponent` which manages its own
 * `ClinicalTimelineFacade` instance for isolated signal state.
 */
@Component({
  selector: 'app-profile-timeline-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ClinicalTimelineComponent],
  template: `<app-clinical-timeline [patientId]="patientId()" />`,
  styles: [`:host { display: block; padding: 8px 0; }`],
})
export class ProfileTimelineTabComponent {
  readonly patientId = input.required<string>();
}
