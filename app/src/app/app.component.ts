import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MainLayoutComponent } from './layouts/main-layout/main-layout.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MainLayoutComponent],
  // AppComponent delegates all layout and routing to MainLayoutComponent.
  // MainLayoutComponent owns the toolbar, sidenav, and router-outlet.
  template: '<app-main-layout />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  readonly title = 'propeliq-app';
}
