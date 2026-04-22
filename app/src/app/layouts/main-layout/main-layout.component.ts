import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { InactivityTimerService } from '../../core/services/inactivity-timer.service';
import { SessionSignalRService } from '../../core/services/session-signalr.service';
import { SessionTimeoutModalComponent } from '../../shared/components/session-timeout-modal/session-timeout-modal.component';

interface NavItem {
  readonly label: string;
  readonly icon: string;
  readonly route: string;
}

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    SessionTimeoutModalComponent,
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainLayoutComponent implements OnInit {
  private readonly inactivityTimer = inject(InactivityTimerService);
  private readonly sessionSignalR = inject(SessionSignalRService);

  protected readonly sidenavOpen = signal(true);

  protected readonly navItems: readonly NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
  ];

  ngOnInit(): void {
    // Start session-tracking services when authenticated layout is active (us_017).
    this.inactivityTimer.start();
    this.sessionSignalR.start();
  }

  protected toggleSidenav(): void {
    this.sidenavOpen.update((open) => !open);
  }
}
