import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * 404 Not Found component rendered when the router matches the wildcard route.
 * Satisfies US_001 Edge Case: undefined routes are handled gracefully.
 */
@Component({
  selector: 'app-not-found',
  standalone: true,
  template: `
    <main role="main" aria-labelledby="not-found-heading">
      <h1 id="not-found-heading">Page Not Found</h1>
      <p>The page you are looking for does not exist.</p>
      <a href="/" aria-label="Return to dashboard">Return to dashboard</a>
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundComponent {}
