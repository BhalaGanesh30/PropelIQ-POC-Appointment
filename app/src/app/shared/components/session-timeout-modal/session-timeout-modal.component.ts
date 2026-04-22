import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  ViewChild,
  computed,
  effect,
  inject,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { InactivityTimerService } from '../../../core/services/inactivity-timer.service';
import { AuthService } from '../../../features/auth/services/auth.service';

/**
 * Non-blocking session-timeout warning modal (us_017 AC-1, UXR-102).
 *
 * Rendered globally in MainLayoutComponent so it overlays the entire app.
 * Becomes visible when `InactivityTimerService.showWarning` is `true` (13 min idle).
 * Provides "Extend Session" (AC-4) and "Logout" actions.
 *
 * Accessibility:
 *   - `role="dialog"` + `aria-modal="true"` (UXR-206)
 *   - Focus moves to the primary action button when the modal appears (UXR-206)
 *   - Tab key is trapped between the two buttons (UXR-206)
 *   - Escape key is prevented so the user must actively choose (UXR-102)
 *   - Countdown announced to screen readers at 60 s, 30 s, 10 s (UXR-203)
 */
@Component({
  selector: 'app-session-timeout-modal',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './session-timeout-modal.component.html',
  styleUrl: './session-timeout-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionTimeoutModalComponent {
  private readonly inactivityTimer = inject(InactivityTimerService);
  private readonly authService = inject(AuthService);

  @ViewChild('extendBtn') extendBtnRef?: ElementRef<HTMLButtonElement>;

  readonly showWarning = this.inactivityTimer.showWarning;
  readonly countdownSeconds = this.inactivityTimer.countdownSeconds;

  /** MM:SS formatted countdown string. */
  readonly formattedCountdown = computed(() => {
    const s = this.countdownSeconds();
    const min = Math.floor(s / 60);
    const sec = s % 60;
    return `${min}:${sec.toString().padStart(2, '0')}`;
  });

  /** Polite screen-reader announcement text at key intervals (UXR-203). */
  readonly srAnnouncement = computed(() => {
    const s = this.countdownSeconds();
    if (s === 60) return 'One minute remaining before your session expires.';
    if (s === 30) return 'Thirty seconds remaining before your session expires.';
    if (s === 10) return 'Ten seconds remaining before your session expires.';
    return '';
  });

  constructor() {
    // Move focus to the primary action when the modal becomes visible (UXR-206).
    effect(() => {
      if (this.showWarning()) {
        // Defer one microtask to ensure the DOM is rendered.
        Promise.resolve().then(() =>
          this.extendBtnRef?.nativeElement?.focus(),
        );
      }
    });
  }

  onExtendSession(): void {
    this.authService.extendSession();
    this.inactivityTimer.resetTimer();
  }

  onLogout(): void {
    this.inactivityTimer.stop();
    this.authService.forceLogout('session-expired');
  }

  /** Tab-trap and Escape prevention within the modal dialog (UXR-206). */
  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      return;
    }

    if (event.key !== 'Tab') return;

    const container = event.currentTarget as HTMLElement;
    const focusable = Array.from(
      container.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [tabindex]:not([tabindex="-1"])',
      ),
    );
    if (focusable.length < 2) return;

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
}
