import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { InactivityTimerService } from '../../core/services/inactivity-timer.service';
import { SessionSignalRService } from '../../core/services/session-signalr.service';
import { SessionTimeoutModalComponent } from '../../shared/components/session-timeout-modal/session-timeout-modal.component';
import { AuthService } from '../../features/auth/services/auth.service';
import { TokenStorageService } from '../../core/services/token-storage.service';

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
    MatDividerModule,
    MatTooltipModule,
    SessionTimeoutModalComponent,
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainLayoutComponent implements OnInit {
  private readonly inactivityTimer = inject(InactivityTimerService);
  private readonly sessionSignalR = inject(SessionSignalRService);
  private readonly authService = inject(AuthService);
  private readonly tokenStorage = inject(TokenStorageService);

  protected readonly sidenavOpen = signal(true);

  /** True when the user is authenticated — drives sidenav visibility.
   *  Sidenav auto-closes on logout and auto-opens on login. */
  protected readonly isAuthenticated = computed(() =>
    this.tokenStorage.isAuthenticated(),
  );

  /** Combined: sidenav is open only when the user is logged in AND hasn't manually closed it. */
  protected readonly sidenavVisible = computed(
    () => this.isAuthenticated() && this.sidenavOpen(),
  );

  /** When true the sidenav shrinks to 64 px icon-only (mini-rail) mode. */
  protected readonly sidenavCollapsed = signal(false);

  protected readonly navItems: readonly NavItem[] = [
    { label: 'Dashboard',        icon: 'dashboard',     route: '/dashboard' },
    { label: 'Find Appointment', icon: 'calendar_today', route: '/scheduling/search' },
    { label: 'My Appointments',  icon: 'event_note',    route: '/appointments' },
    { label: 'My Waitlist',      icon: 'queue',         route: '/waitlist' },
  ];

  protected readonly staffNavItems: readonly NavItem[] = [
    { label: 'Real-Time Queue',    icon: 'view_list',       route: '/staff/queue' },
    { label: 'Daily Schedule',     icon: 'calendar_view_day', route: '/staff/schedule' },
    { label: 'Book for Patient',   icon: 'book_online',     route: '/staff/booking' },
    { label: 'Walk-In',            icon: 'directions_walk', route: '/staff/walkin' },
    { label: 'Risk Scores',        icon: 'assessment',   route: '/queue' },
    { label: 'Notifications',      icon: 'notifications', route: '/settings/notifications' },
  ];

  protected readonly adminNavItems: readonly NavItem[] = [
    { label: 'User Management', icon: 'manage_accounts', route: '/admin/users' },
  ];

  /** True for Staff, Admin, and Clinician roles — gates the staff nav section. */
  protected readonly isStaff = computed(() => {
    if (!this.tokenStorage.isAuthenticated()) return false;
    const role = this.tokenStorage.getUserRole();
    return role === 'Staff' || role === 'Admin' || role === 'Clinician';
  });

  protected readonly isAdmin = computed(() => {
    // Re-evaluates when isAuthenticated signal changes (login/logout).
    if (!this.tokenStorage.isAuthenticated()) return false;
    const role = this.tokenStorage.getUserRole();
    return role === 'Admin' || role === 'SuperAdmin';
  });

  protected readonly userInitials = computed(() => {
    if (!this.tokenStorage.isAuthenticated()) return 'U';
    const decoded = this.tokenStorage.getDecodedToken();
    const first = (decoded?.['given_name'] as string | undefined) ?? '';
    const last  = (decoded?.['family_name'] as string | undefined) ?? '';
    if (first && last) return `${first[0]}${last[0]}`.toUpperCase();
    if (first) return first.substring(0, 2).toUpperCase();
    const email = (decoded?.['email'] as string | undefined) ?? '';
    return email ? email[0].toUpperCase() : 'U';
  });

  protected readonly userDisplayName = computed(() => {
    if (!this.tokenStorage.isAuthenticated()) return '';
    const decoded = this.tokenStorage.getDecodedToken();
    const first = (decoded?.['given_name'] as string | undefined) ?? '';
    const last  = (decoded?.['family_name'] as string | undefined) ?? '';
    return ([first, last].filter(Boolean).join(' ') ||
      (decoded?.['email'] as string | undefined)) ?? 'User';
  });

  protected readonly userRole = computed(() => {
    if (!this.tokenStorage.isAuthenticated()) return null;
    return this.tokenStorage.getUserRole();
  });

  ngOnInit(): void {
    this.inactivityTimer.start();
    this.sessionSignalR.start();
  }

  protected toggleSidenav(): void {
    this.sidenavCollapsed.update((c) => !c);
  }

  protected logout(): void {
    this.inactivityTimer.stop();
    this.authService.forceLogout('session-ended');
  }
}
