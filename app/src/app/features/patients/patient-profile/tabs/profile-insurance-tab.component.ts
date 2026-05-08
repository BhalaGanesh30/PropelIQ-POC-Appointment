import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyProfileStateComponent } from '../shared/empty-profile-state.component';

/** Insurance tab stub — post-MVP placeholder. */
@Component({
  selector: 'app-profile-insurance-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyProfileStateComponent],
  template: `
    <app-empty-profile-state
      message="Insurance eligibility integration coming soon."
      icon="health_and_safety"
    />
  `,
  styles: [`:host { display: block; padding: 8px 0; }`],
})
export class ProfileInsuranceTabComponent {}
