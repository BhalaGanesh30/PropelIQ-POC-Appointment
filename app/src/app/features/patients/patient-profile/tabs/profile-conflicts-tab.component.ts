import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { ConflictAlertsComponent } from '../conflict-alerts/conflict-alerts.component';

/**
 * Tab wrapper for the Conflicts tab in the 360° patient profile (SCR-016).
 *
 * ConflictAlertsFacade is intentionally NOT provided here — it is provided by
 * PatientProfileComponent so that the tab-switch guard in that component can
 * observe `conflictsFacade.pendingCritical()` from the same instance (AC-3).
 *
 * Lazy loading: ConflictAlertsComponent calls `facade.loadConflicts()` on init,
 * matching the lazy per-tab pattern used by the other profile tabs.
 */
@Component({
  selector: 'app-profile-conflicts-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ConflictAlertsComponent],
  template: `<app-conflict-alerts [patientId]="patientId()" />`,
  styles: [`:host { display: block; padding: 8px 0; }`],
})
export class ProfileConflictsTabComponent {
  readonly patientId = input.required<string>();
}
