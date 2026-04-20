import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Dashboard placeholder component.
 * Replace with the real dashboard feature implementation once EP-XXX tasks are complete.
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <h1>Dashboard</h1>
    <p>Welcome to PropelIQ.</p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {}
