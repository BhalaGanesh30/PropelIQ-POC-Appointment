import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MainLayoutComponent } from './layouts/main-layout/main-layout.component';
import { AiGatewayStatusFacade } from './shared/facades/ai-gateway-status.facade';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MainLayoutComponent],
  // AppComponent delegates all layout and routing to MainLayoutComponent.
  // MainLayoutComponent owns the toolbar, sidenav, and router-outlet.
  template: '<app-main-layout />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit {
  readonly title = 'propeliq-app';
  private readonly aiStatusFacade = inject(AiGatewayStatusFacade);

  ngOnInit(): void {
    // Initialise AI gateway circuit breaker polling (US_053, AC-2, AC-3).
    // Performs an initial status check; if the circuit is open/half-open, a 30-second
    // polling loop starts automatically and stops when the circuit returns 'closed'.
    this.aiStatusFacade.initialize();
  }
}
