import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

/**
 * Placeholder login component.
 * Render target for authGuard redirects during scaffold phase.
 * Replace with full authentication form in EP-001.
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [MatButtonModule, RouterLink],
  template: `
    <main role="main" class="login-container" aria-labelledby="login-heading">
      <h1 id="login-heading">Sign In</h1>
      <p>Authentication will be implemented in EP-001.</p>
      <a mat-raised-button color="primary" routerLink="/dashboard">
        Continue to Dashboard
      </a>
    </main>
  `,
  styles: [
    `
      .login-container {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        min-height: 100vh;
        gap: 16px;
        padding: 24px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {}
