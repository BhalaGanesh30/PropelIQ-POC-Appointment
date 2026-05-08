import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyProfileStateComponent } from '../shared/empty-profile-state.component';

/** Coding tab stub — ICD-10/CPT coding suggestions post-MVP placeholder. */
@Component({
  selector: 'app-profile-coding-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyProfileStateComponent],
  template: `
    <app-empty-profile-state
      message="AI-assisted coding suggestions coming soon."
      icon="code"
    />
  `,
  styles: [`:host { display: block; padding: 8px 0; }`],
})
export class ProfileCodingTabComponent {}
