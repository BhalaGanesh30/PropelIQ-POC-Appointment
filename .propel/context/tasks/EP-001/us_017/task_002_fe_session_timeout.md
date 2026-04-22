# Task - TASK_002

## Requirement Reference

- User Story: us_017
- Story Location: .propel/context/tasks/EP-001/us_017/us_017.md
- Acceptance Criteria:
  - AC-1: Given a user is authenticated and has been inactive for 13 minutes, When the 13-minute inactivity threshold is reached, Then a non-blocking modal appears with a 2-minute countdown offering "Extend Session" and "Logout" options.
  - AC-2: Given the session warning modal is shown, When the 2-minute countdown expires without user action, Then the session is terminated, the JWT is revoked, and the user is redirected to the login page with the message "Session expired."
  - AC-3: Given a user logs in from a second device while an active session exists, When the second login is processed, Then the first session is immediately invalidated and the user on the first device receives a "Session ended" notification.
  - AC-4: Given a user clicks "Extend Session" in the warning modal, When the extension request is processed, Then the inactivity timer resets to 15 minutes and the modal is dismissed.
- Edge Cases:
  - What happens if the user's browser crashes during an active session? Session times out after 15 minutes of inactivity from the last recorded activity timestamp.
  - How does the system handle tab duplication in the same browser? Both tabs share the same session; activity in either tab resets the inactivity timer.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | TODO: Provide wireframe - upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-030-session-timeout.[html\|png\|jpg]` or add external URL |
| **Screen Spec** | figma_spec.md#SCR-030 (Validation state — session timeout warning modal) |
| **UXR Requirements** | UXR-102, UXR-201, UXR-203, UXR-206 |
| **Design Tokens** | designsystem.md — colors, typography, spacing |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | N/A (consumed via API) | N/A |
| Database | N/A | N/A |
| Library | Angular Material | 17.x |
| Library | @angular/router | 17.x (bundled) |
| Library | @microsoft/signalr | latest stable |
| Library | rxjs | 7.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement the Angular 17 client-side session timeout system with inactivity tracking, cross-tab synchronization, a 2-minute warning countdown modal, automatic session termination, and real-time SignalR notification handling for single-session enforcement. The `InactivityTimerService` tracks user activity events (mouse, keyboard, scroll, touch) and uses `BroadcastChannel` API to synchronize the last-activity timestamp across browser tabs sharing the same session (edge case: tab duplication). At 13 minutes of inactivity, a non-blocking `SessionTimeoutModalComponent` renders as an overlay with a live 2-minute countdown timer, "Extend Session" and "Logout" action buttons (AC-1, UXR-102). Clicking "Extend Session" calls `POST /api/v1/auth/session/extend`, resets the inactivity timer to 15 minutes, and dismisses the modal (AC-4). If the countdown reaches zero without action, the service calls the logout endpoint, clears local tokens, and redirects to `/auth/login` with a "Session expired" query parameter (AC-2). A `SessionSignalRService` establishes an authenticated SignalR connection to the `SessionHub` and listens for "SessionEnded" events, displaying a non-dismissable notification and redirecting to login (AC-3). Focus trapping is enforced within the modal per UXR-206, and dynamic content updates announce countdown changes to screen readers per UXR-203.

## Dependent Tasks

- US_014 task_002 (requires Angular auth infrastructure: TokenStorageService, AuthService, AuthInterceptor, auth routing)
- US_017 task_001 (requires backend session management API, SignalR hub, session extend endpoint)

## Impacted Components

- New: `client/src/app/core/services/inactivity-timer.service.ts` (tracks user activity, manages 13-min warning and 15-min timeout thresholds)
- New: `client/src/app/core/services/session-signalr.service.ts` (SignalR client for session hub, handles SessionEnded events)
- New: `client/src/app/shared/components/session-timeout-modal/session-timeout-modal.component.ts` (countdown modal with extend/logout actions)
- New: `client/src/app/shared/components/session-timeout-modal/session-timeout-modal.component.html` (modal template with countdown display and action buttons)
- New: `client/src/app/shared/components/session-timeout-modal/session-timeout-modal.component.scss` (modal overlay and content styles)
- Modify: `client/src/app/app.component.ts` (inject InactivityTimerService and SessionSignalRService initialization)
- Modify: `client/src/app/features/auth/services/auth.service.ts` (add extendSession and handleSessionExpired methods)
- Modify: `client/src/app/core/services/token-storage.service.ts` (add sessionToken storage and retrieval)

## Implementation Plan

1. **Create `InactivityTimerService`** for tracking user inactivity with cross-tab synchronization via `BroadcastChannel`:

```typescript
// client/src/app/core/services/inactivity-timer.service.ts
import { Injectable, NgZone, OnDestroy, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, fromEvent, merge, takeUntil, throttleTime } from 'rxjs';

const WARNING_THRESHOLD_MS = 13 * 60 * 1000; // 13 minutes
const TIMEOUT_THRESHOLD_MS = 15 * 60 * 1000; // 15 minutes
const COUNTDOWN_DURATION_S = 120;             // 2 minutes
const ACTIVITY_THROTTLE_MS = 30_000;          // Throttle activity events
const BROADCAST_CHANNEL = 'propeliq_session_activity';
const STORAGE_KEY = 'propeliq_last_activity';

@Injectable({ providedIn: 'root' })
export class InactivityTimerService implements OnDestroy {
  showWarning = signal(false);
  countdownSeconds = signal(COUNTDOWN_DURATION_S);

  private destroy$ = new Subject<void>();
  private warningTimer: ReturnType<typeof setTimeout> | null = null;
  private countdownInterval: ReturnType<typeof setInterval> | null = null;
  private channel: BroadcastChannel | null = null;
  private isRunning = false;

  constructor(
    private ngZone: NgZone,
    private router: Router
  ) {}

  start(): void {
    if (this.isRunning) return;
    this.isRunning = true;

    // Cross-tab synchronization via BroadcastChannel
    if ('BroadcastChannel' in globalThis) {
      this.channel = new BroadcastChannel(BROADCAST_CHANNEL);
      this.channel.onmessage = (event) => {
        if (event.data === 'activity') {
          this.resetTimerInternal(false);
        }
      };
    }

    // Track user activity events outside Angular zone for performance
    this.ngZone.runOutsideAngular(() => {
      const activity$ = merge(
        fromEvent(document, 'mousemove'),
        fromEvent(document, 'keydown'),
        fromEvent(document, 'scroll'),
        fromEvent(document, 'touchstart'),
        fromEvent(document, 'click')
      ).pipe(
        throttleTime(ACTIVITY_THROTTLE_MS),
        takeUntil(this.destroy$)
      );

      activity$.subscribe(() => {
        this.onUserActivity();
      });
    });

    this.recordActivity();
    this.startWarningTimer();
  }

  stop(): void {
    this.isRunning = false;
    this.clearTimers();
    this.showWarning.set(false);
    this.countdownSeconds.set(COUNTDOWN_DURATION_S);
    this.channel?.close();
    this.channel = null;
    this.destroy$.next();
  }

  resetTimer(): void {
    this.resetTimerInternal(true);
  }

  private resetTimerInternal(broadcast: boolean): void {
    this.clearTimers();
    this.showWarning.set(false);
    this.countdownSeconds.set(COUNTDOWN_DURATION_S);
    this.recordActivity();
    this.startWarningTimer();

    if (broadcast) {
      this.channel?.postMessage('activity');
    }
  }

  private onUserActivity(): void {
    // Only reset if warning is not already showing
    if (!this.showWarning()) {
      this.resetTimerInternal(true);
    }
  }

  private recordActivity(): void {
    localStorage.setItem(STORAGE_KEY, Date.now().toString());
  }

  private startWarningTimer(): void {
    this.warningTimer = setTimeout(() => {
      this.ngZone.run(() => {
        this.showWarning.set(true);
        this.startCountdown();
      });
    }, WARNING_THRESHOLD_MS);
  }

  private startCountdown(): void {
    this.countdownSeconds.set(COUNTDOWN_DURATION_S);

    this.countdownInterval = setInterval(() => {
      this.ngZone.run(() => {
        const remaining = this.countdownSeconds() - 1;
        this.countdownSeconds.set(remaining);

        if (remaining <= 0) {
          this.onSessionExpired();
        }
      });
    }, 1000);
  }

  private onSessionExpired(): void {
    this.stop();
    this.router.navigate(['/auth/login'], {
      queryParams: { reason: 'session-expired' }
    });
  }

  private clearTimers(): void {
    if (this.warningTimer) {
      clearTimeout(this.warningTimer);
      this.warningTimer = null;
    }
    if (this.countdownInterval) {
      clearInterval(this.countdownInterval);
      this.countdownInterval = null;
    }
  }

  ngOnDestroy(): void {
    this.stop();
    this.destroy$.complete();
  }
}
```

2. **Create `SessionSignalRService`** for real-time session invalidation notifications:

```typescript
// client/src/app/core/services/session-signalr.service.ts
import { Injectable, NgZone } from '@angular/core';
import { Router } from '@angular/router';
import * as signalR from '@microsoft/signalr';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class SessionSignalRService {
  private connection: signalR.HubConnection | null = null;

  constructor(
    private tokenStorage: TokenStorageService,
    private router: Router,
    private ngZone: NgZone
  ) {}

  start(): void {
    const accessToken = this.tokenStorage.getAccessToken();
    if (!accessToken || this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/session', {
        accessTokenFactory: () =>
          this.tokenStorage.getAccessToken() ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.connection.on('SessionEnded', (message: string) => {
      this.ngZone.run(() => {
        this.tokenStorage.clearTokens();
        this.stop();
        this.router.navigate(['/auth/login'], {
          queryParams: { reason: 'session-ended' }
        });
      });
    });

    this.connection.start().catch(err =>
      console.error('SignalR connection error:', err)
    );
  }

  stop(): void {
    this.connection?.stop();
    this.connection = null;
  }
}
```

3. **Create `SessionTimeoutModalComponent`** as a non-blocking overlay with 2-minute countdown, "Extend Session" and "Logout" buttons, focus trapping (UXR-206), and screen reader announcements (UXR-203):

```typescript
// client/src/app/shared/components/session-timeout-modal/
//   session-timeout-modal.component.ts
import {
  Component,
  ChangeDetectionStrategy,
  inject,
  effect,
  ElementRef,
  ViewChild,
  AfterViewInit,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { InactivityTimerService } from '../../../core/services/inactivity-timer.service';
import { AuthService } from '../../../features/auth/services/auth.service';

@Component({
  selector: 'app-session-timeout-modal',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './session-timeout-modal.component.html',
  styleUrls: ['./session-timeout-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionTimeoutModalComponent implements AfterViewInit {
  private inactivityTimer = inject(InactivityTimerService);
  private authService = inject(AuthService);

  @ViewChild('extendButton') extendButtonRef!: ElementRef<HTMLButtonElement>;

  showWarning = this.inactivityTimer.showWarning;
  countdownSeconds = this.inactivityTimer.countdownSeconds;

  private focusTrapEffect = effect(() => {
    if (this.showWarning()) {
      // Move focus into modal when shown (UXR-206)
      setTimeout(() =>
        this.extendButtonRef?.nativeElement?.focus(), 0);
    }
  });

  ngAfterViewInit(): void {
    // Focus trap handled via effect above
  }

  get formattedCountdown(): string {
    const seconds = this.countdownSeconds();
    const min = Math.floor(seconds / 60);
    const sec = seconds % 60;
    return `${min}:${sec.toString().padStart(2, '0')}`;
  }

  get screenReaderAnnouncement(): string {
    const seconds = this.countdownSeconds();
    if (seconds === 60) return 'One minute remaining before session expires.';
    if (seconds === 30) return 'Thirty seconds remaining before session expires.';
    if (seconds === 10) return 'Ten seconds remaining before session expires.';
    return '';
  }

  onExtendSession(): void {
    this.authService.extendSession();
    this.inactivityTimer.resetTimer();
  }

  onLogout(): void {
    this.authService.logout();
  }

  onKeyDown(event: KeyboardEvent): void {
    // Trap focus within modal (UXR-206)
    if (event.key === 'Tab') {
      const focusable = (event.currentTarget as HTMLElement)
        .querySelectorAll<HTMLElement>(
          'button, [tabindex]:not([tabindex="-1"])'
        );
      const first = focusable[0];
      const last = focusable[focusable.length - 1];

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    // Prevent Escape from dismissing — user must choose an action
    if (event.key === 'Escape') {
      event.preventDefault();
    }
  }
}
```

4. **Create the modal template** with countdown, action buttons, and accessibility attributes:

```html
<!-- session-timeout-modal.component.html -->
@if (showWarning()) {
  <div
    class="session-timeout-overlay"
    role="dialog"
    aria-modal="true"
    aria-labelledby="session-timeout-title"
    aria-describedby="session-timeout-desc"
    (keydown)="onKeyDown($event)">
    <div class="session-timeout-modal">
      <div class="modal-icon">
        <mat-icon class="warning-icon">schedule</mat-icon>
      </div>

      <h2 id="session-timeout-title">Session Expiring</h2>

      <p id="session-timeout-desc">
        Your session will expire in
        <span class="countdown" aria-live="assertive">
          {{ formattedCountdown }}
        </span>.
        Would you like to continue?
      </p>

      <!-- Screen reader announcements at key intervals (UXR-203) -->
      <div
        class="sr-only"
        role="status"
        aria-live="polite"
        aria-atomic="true">
        {{ screenReaderAnnouncement }}
      </div>

      <div class="modal-actions">
        <button
          #extendButton
          mat-flat-button
          color="primary"
          (click)="onExtendSession()"
          aria-label="Extend session for 15 more minutes">
          Extend Session
        </button>
        <button
          mat-stroked-button
          (click)="onLogout()"
          aria-label="Log out now">
          Logout
        </button>
      </div>
    </div>
  </div>
}
```

5. **Create the modal styles** with overlay, centered card, and countdown emphasis:

```scss
// session-timeout-modal.component.scss
.session-timeout-overlay {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(2px);
}

.session-timeout-modal {
  background: var(--mat-sys-surface, #fff);
  border-radius: 12px;
  padding: 32px;
  max-width: 400px;
  width: 90%;
  text-align: center;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.24);
}

.modal-icon {
  margin-bottom: 16px;

  .warning-icon {
    font-size: 48px;
    width: 48px;
    height: 48px;
    color: var(--mat-sys-warning, #f59e0b);
  }
}

h2 {
  margin: 0 0 8px;
  font-size: 1.25rem;
  font-weight: 600;
  color: var(--mat-sys-on-surface, #1a1a1a);
}

p {
  margin: 0 0 24px;
  color: var(--mat-sys-on-surface-variant, #555);
  line-height: 1.5;
}

.countdown {
  font-weight: 700;
  font-size: 1.125rem;
  font-variant-numeric: tabular-nums;
  color: var(--mat-sys-error, #dc2626);
}

.modal-actions {
  display: flex;
  gap: 12px;
  justify-content: center;

  button {
    min-width: 140px;
  }
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
```

6. **Modify `AuthService`** to add session extend and session-expired handling:

```typescript
// Add to client/src/app/features/auth/services/auth.service.ts

extendSession(): void {
  const sessionToken = this.tokenStorage.getSessionToken();
  if (!sessionToken) return;

  this.http.post<{ expiresInSeconds: number }>(
    '/api/v1/auth/session/extend',
    { sessionToken }
  ).subscribe({
    error: () => {
      // If extend fails, force logout
      this.logout();
    }
  });
}

handleSessionExpired(): void {
  this.tokenStorage.clearTokens();
  this.router.navigate(['/auth/login'], {
    queryParams: { reason: 'session-expired' }
  });
}
```

7. **Modify `TokenStorageService`** to include session token persistence:

```typescript
// Add to client/src/app/core/services/token-storage.service.ts

private static readonly SESSION_TOKEN_KEY = 'propeliq_session_token';

saveSessionToken(sessionToken: string): void {
  this.storage.setItem(TokenStorageService.SESSION_TOKEN_KEY, sessionToken);
}

getSessionToken(): string | null {
  return localStorage.getItem(TokenStorageService.SESSION_TOKEN_KEY)
    ?? sessionStorage.getItem(TokenStorageService.SESSION_TOKEN_KEY);
}

// Update clearTokens to also remove session token
// Add to existing clearTokens method:
//   localStorage.removeItem(SESSION_TOKEN_KEY);
//   sessionStorage.removeItem(SESSION_TOKEN_KEY);
```

8. **Modify `AppComponent`** to initialize session services after authentication:

```typescript
// Add to client/src/app/app.component.ts
import { Component, OnInit, inject } from '@angular/core';
import { InactivityTimerService } from './core/services/inactivity-timer.service';
import { SessionSignalRService } from './core/services/session-signalr.service';
import { TokenStorageService } from './core/services/token-storage.service';
import { SessionTimeoutModalComponent } from
  './shared/components/session-timeout-modal/session-timeout-modal.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SessionTimeoutModalComponent],
  template: `
    <router-outlet />
    <app-session-timeout-modal />
  `
})
export class AppComponent implements OnInit {
  private inactivityTimer = inject(InactivityTimerService);
  private signalRService = inject(SessionSignalRService);
  private tokenStorage = inject(TokenStorageService);

  ngOnInit(): void {
    if (this.tokenStorage.isAuthenticated()) {
      this.inactivityTimer.start();
      this.signalRService.start();
    }
  }
}
```

9. **Handle login page "Session expired" / "Session ended" messages**:

```typescript
// In login.component.ts — read query params
import { ActivatedRoute } from '@angular/router';

// In constructor or ngOnInit:
const reason = this.route.snapshot.queryParamMap.get('reason');
if (reason === 'session-expired') {
  this.bannerMessage = 'Session expired.';
} else if (reason === 'session-ended') {
  this.bannerMessage = 'Session ended.';
}
```

10. **Install SignalR client package**:

```bash
cd client
npm install @microsoft/signalr
```

## Current Project State

```text
propelIQ/
├── client/
│   └── src/
│       └── app/
│           ├── app.component.ts
│           ├── app.config.ts
│           ├── core/
│           │   ├── interceptors/
│           │   │   └── auth.interceptor.ts         (from US_014 task_002)
│           │   ├── guards/
│           │   │   └── auth.guard.ts               (from US_014 task_002)
│           │   └── services/
│           │       └── token-storage.service.ts     (from US_014 task_002)
│           ├── features/
│           │   └── auth/
│           │       ├── services/
│           │       │   └── auth.service.ts          (from US_014 task_002)
│           │       ├── auth-routing.module.ts
│           │       └── pages/
│           │           └── login/
│           │               ├── login.component.ts   (from US_014 task_002)
│           │               ├── login.component.html
│           │               └── login.component.scss
│           └── shared/
│               └── components/
└── server/
    └── src/
        └── PropelIQ.Api/
            └── Hubs/
                └── SessionHub.cs                    (from US_017 task_001)
```

> Placeholder: Update on execution based on US_014 task_002 and US_017 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/core/services/inactivity-timer.service.ts | User activity tracking with cross-tab BroadcastChannel sync, 13-min warning trigger, 2-min countdown |
| CREATE | client/src/app/core/services/session-signalr.service.ts | SignalR client connecting to SessionHub, handles SessionEnded events with redirect |
| CREATE | client/src/app/shared/components/session-timeout-modal/session-timeout-modal.component.ts | Standalone component with countdown display, extend/logout actions, focus trapping |
| CREATE | client/src/app/shared/components/session-timeout-modal/session-timeout-modal.component.html | Modal template with ARIA attributes, live countdown region, action buttons |
| CREATE | client/src/app/shared/components/session-timeout-modal/session-timeout-modal.component.scss | Overlay styles, centered modal card, countdown emphasis, sr-only utility |
| MODIFY | client/src/app/app.component.ts | Import SessionTimeoutModalComponent, initialize InactivityTimerService and SessionSignalRService on auth |
| MODIFY | client/src/app/features/auth/services/auth.service.ts | Add extendSession() and handleSessionExpired() methods |
| MODIFY | client/src/app/core/services/token-storage.service.ts | Add sessionToken save/get/clear for session management |
| MODIFY | client/src/app/features/auth/pages/login/login.component.ts | Read query params for session-expired and session-ended banner messages |
| INSTALL | @microsoft/signalr | SignalR JavaScript client for session hub connection |

## External References

- Angular Signals: https://angular.dev/guide/signals
- BroadcastChannel API: https://developer.mozilla.org/en-US/docs/Web/API/BroadcastChannel
- SignalR JavaScript client: https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client
- WAI-ARIA Authoring Practices — Dialog (Modal): https://www.w3.org/WAI/ARIA/apd/patterns/dialog-modal/
- WCAG 2.1 Success Criterion 4.1.3 — Status Messages: https://www.w3.org/WAI/WCAG21/Understanding/status-messages
- Angular Material Dialog accessibility: https://material.angular.io/components/dialog/overview

## Build Commands

```bash
# Install dependencies
cd client
npm install @microsoft/signalr

# Build frontend
ng build

# Serve frontend
ng serve

# Run tests
ng test
```

## Implementation Validation Strategy

- [ ] Warning modal appears after exactly 13 minutes of inactivity with a 2:00 countdown (AC-1)
- [ ] "Extend Session" button calls `/api/v1/auth/session/extend`, resets timer to 15 minutes, and dismisses modal (AC-4)
- [ ] "Logout" button clears tokens and redirects to `/auth/login` (AC-1)
- [ ] Countdown reaching 0:00 triggers automatic logout with redirect to `/auth/login?reason=session-expired` (AC-2)
- [ ] Login page displays "Session expired." banner when `reason=session-expired` query param is present (AC-2)
- [ ] Login page displays "Session ended." banner when `reason=session-ended` query param is present (AC-3)
- [ ] SignalR receives "SessionEnded" event and redirects to `/auth/login?reason=session-ended` (AC-3)
- [ ] User activity (mouse, keyboard, scroll, touch) resets inactivity timer when warning is not showing
- [ ] Activity in one tab resets the timer in other tabs via BroadcastChannel (edge case: tab duplication)
- [ ] Modal traps focus between "Extend Session" and "Logout" buttons (UXR-206)
- [ ] Countdown changes announce at 1:00, 0:30, and 0:10 to screen readers via `aria-live` region (UXR-203)
- [ ] Modal meets WCAG 2.1 AA color contrast requirements (UXR-201)
- [ ] Session services initialize only when user is authenticated
- [ ] Session services clean up on logout (timers cleared, SignalR disconnected)

## Implementation Checklist

- [x] Install `@microsoft/signalr` npm package
- [x] Create `InactivityTimerService` with `start()`, `stop()`, `resetTimer()`, activity event tracking, and `BroadcastChannel` sync
- [x] Create `SessionSignalRService` with authenticated hub connection, `SessionEnded` handler, and auto-reconnect
- [x] Create `SessionTimeoutModalComponent` standalone component with countdown display and action buttons
- [x] Create modal HTML template with `role="dialog"`, `aria-modal="true"`, `aria-live` countdown region, and `aria-labelledby`/`aria-describedby`
- [x] Create modal SCSS with overlay, centered card, tabular-nums countdown, and `.sr-only` utility class
- [x] Implement focus trapping within modal using `keydown` Tab handler (UXR-206)
- [x] Implement screen reader announcements at 60s, 30s, and 10s countdown intervals (UXR-203)
- [x] Add `extendSession()` method to `AuthService` calling `POST /api/v1/auth/session/extend`
- [x] Add `sessionToken` storage methods to `TokenStorageService`
- [x] Update `MainLayoutComponent` to import modal and initialize session services on authenticated state
- [x] Add session-expired and session-ended banner message handling to `LoginComponent`
- [ ] Verify cross-tab activity sync resets warning timer in all same-origin tabs
