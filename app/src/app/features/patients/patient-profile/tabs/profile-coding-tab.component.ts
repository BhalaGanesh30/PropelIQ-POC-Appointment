import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CodingSuggestionPanelComponent } from '../coding-suggestion-panel/coding-suggestion-panel.component';

/**
 * Coding tab wrapper for the 360° patient profile (SCR-017 / US_049).
 * Passes the patient ID to CodingSuggestionPanelComponent which manages its own state.
 */
@Component({
  selector: 'app-profile-coding-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CodingSuggestionPanelComponent],
  template: `
    <app-coding-suggestion-panel [patientId]="patientId()" />
  `,
  styles: [`:host { display: block; padding: 8px 0; }`],
})
export class ProfileCodingTabComponent {
  readonly patientId = input.required<string>();
}
