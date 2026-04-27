import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <h1>Dashboard</h1>
    <p>Welcome to PropelIQ.</p>
    <a routerLink="/scheduling/search">Find an Appointment</a>
    <br />
    <a routerLink="/appointments">My Appointments</a>
    <br />
    <a routerLink="/waitlist">My Waitlist</a>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {}
