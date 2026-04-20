import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Shell layout component. Acts as the primary layout wrapper providing
 * the navigation chrome (header, sidebar) around lazy-loaded feature routes.
 * Import this component in feature routes that require the authenticated shell.
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="shell-layout">
      <header class="shell-header" role="banner">
        <span>PropelIQ</span>
      </header>
      <div class="shell-content">
        <router-outlet />
      </div>
    </div>
  `,
  styles: [
    `
      .shell-layout {
        display: flex;
        flex-direction: column;
        min-height: 100vh;
      }

      .shell-header {
        height: 64px;
        display: flex;
        align-items: center;
        padding: 0 24px;
        background-color: var(--mat-sys-primary);
        color: var(--mat-sys-on-primary);
      }

      .shell-content {
        flex: 1;
        padding: 24px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {}
