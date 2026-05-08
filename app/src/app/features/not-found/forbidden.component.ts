import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/**
 * Forbidden (403) page — rendered when an authenticated user attempts to access
 * a route they are not authorised to view (e.g. Patient accessing a Staff-only page).
 * Satisfies EP-005 US_039 Edge Case 2.
 */
@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <main class="forbidden-host" role="main" aria-labelledby="forbidden-heading">
      <mat-icon class="forbidden-icon" aria-hidden="true">lock</mat-icon>
      <h1 id="forbidden-heading" class="forbidden-title">Access Denied</h1>
      <p class="forbidden-body">
        You do not have permission to view this page.
        Please contact your administrator if you believe this is an error.
      </p>
      <a mat-raised-button color="primary" routerLink="/dashboard"
         [attr.aria-label]="'Return to dashboard'">
        Return to Dashboard
      </a>
    </main>
  `,
  styles: [`
    .forbidden-host {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 60vh;
      gap: 16px;
      padding: 48px 24px;
      text-align: center;
    }
    .forbidden-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      color: #9e9e9e;
    }
    .forbidden-title {
      font-size: 24px;
      font-weight: 700;
      color: #212121;
      margin: 0;
    }
    .forbidden-body {
      font-size: 15px;
      color: #616161;
      max-width: 400px;
      margin: 0;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForbiddenComponent {}
