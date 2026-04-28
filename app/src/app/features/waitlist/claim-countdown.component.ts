import {
  ChangeDetectionStrategy,
  Component,
  NgZone,
  OnDestroy,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { CountdownUrgency, URGENCY_COLORS } from './models/slot-claim.models';

/**
 * Full-page countdown timer for the slot claim page (US_030 AC-2 / SCR-008).
 *
 * Uses `requestAnimationFrame` outside Angular's zone for smooth 1-second
 * ticks with minimal change-detection overhead (re-enters zone only to update
 * signals once per second).
 *
 * UXR-112 / UXR-404: urgency color shift milestones:
 *   green  — > 1 h  (normal)
 *   amber  — 30 min – 1 h (warning)
 *   red    — < 30 min, CSS pulse (critical)
 *   grey   — 0 (expired)
 *
 * UXR-203: `role="timer"` + `aria-live="polite"` with milestone announcements
 *   at 1 h, 30 min, and 5 min remaining.
 *
 * UXR-201: all urgency colors meet WCAG AA 4.5:1 contrast on white.
 */
@Component({
  selector: 'app-claim-countdown',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './claim-countdown.component.html',
  styleUrl: './claim-countdown.component.scss',
})
export class ClaimCountdownComponent implements OnInit, OnDestroy {
  /** ISO 8601 UTC expiry timestamp — edge case 2: converted to browser timezone. */
  readonly expiresAtUtc = input.required<string>();

  readonly remainingMs  = signal(0);
  readonly expired      = signal(false);

  /** Last tick time (ms) used to throttle to ~1-second increments. */
  private lastTickTime  = 0;
  private rafId         = 0;
  private readonly zone = inject(NgZone);

  // ── Computed display properties ────────────────────────────────────────────

  readonly urgency = computed<CountdownUrgency>(() => {
    const ms = this.remainingMs();
    if (ms <= 0)                  return 'expired';
    if (ms < 30 * 60 * 1_000)    return 'critical';
    if (ms < 60 * 60 * 1_000)    return 'warning';
    return 'normal';
  });

  readonly color = computed(() => URGENCY_COLORS[this.urgency()]);

  readonly display = computed(() => {
    const ms = this.remainingMs();
    if (ms <= 0) return 'Expired';
    const h = Math.floor(ms / 3_600_000);
    const m = Math.floor((ms % 3_600_000) / 60_000);
    const s = Math.floor((ms % 60_000) / 1_000);
    return `${h}h ${String(m).padStart(2, '0')}m ${String(s).padStart(2, '0')}s`;
  });

  /** UXR-203: milestone text for aria-live announcements (read at key thresholds). */
  readonly milestoneAnnouncement = computed(() => {
    const ms = this.remainingMs();
    const m  = Math.floor(ms / 60_000);
    if (m === 60) return '1 hour remaining to claim this slot';
    if (m === 30) return '30 minutes remaining — claim window is closing soon';
    if (m === 5)  return '5 minutes remaining — claim now or lose this slot';
    return '';          // Empty string clears the live region between milestones
  });

  readonly ariaLabel = computed(() =>
    this.expired()
      ? 'Claim window has expired'
      : `Claim window: ${this.display()} remaining`,
  );

  // ── Lifecycle ──────────────────────────────────────────────────────────────

  ngOnInit(): void {
    // Initialise immediately so the countdown is correct on first render.
    this.updateRemaining();
    // Run the rAF loop outside Angular zone — re-enters only on signal updates.
    this.zone.runOutsideAngular(() => this.scheduleTick());
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.rafId);
  }

  // ── rAF tick loop ──────────────────────────────────────────────────────────

  private scheduleTick(): void {
    this.rafId = requestAnimationFrame((timestamp) => {
      // Throttle to 1-second resolution to avoid excessive signal churn.
      if (timestamp - this.lastTickTime >= 1_000 || this.lastTickTime === 0) {
        this.lastTickTime = timestamp;
        const ms = this.updateRemaining();
        if (ms <= 0) return;   // Stop the loop when expired.
      }
      this.scheduleTick();
    });
  }

  /** Recalculates remaining ms and updates signals inside the Angular zone. */
  private updateRemaining(): number {
    const expiry = new Date(this.expiresAtUtc()).getTime();
    const ms     = Math.max(0, expiry - Date.now());

    this.zone.run(() => {
      this.remainingMs.set(ms);
      if (ms <= 0) this.expired.set(true);
    });

    return ms;
  }
}
