import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnDestroy,
  OnInit,
  computed,
  signal,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { CountdownUrgency } from './models/waitlist.models';

/**
 * Real-time countdown timer for waitlist claim windows (UXR-112, SCR-008).
 *
 * Ticks every second. Urgency colours shift at:
 *   green  — > 1 h  remaining
 *   amber  — 30 min – 1 h
 *   red    — < 30 min (CSS pulse animation)
 *   expired — window has closed
 *
 * UXR-203: aria-live="assertive" when urgency reaches red so screen readers
 * immediately announce the change; "polite" otherwise.
 */
@Component({
  selector: 'app-countdown-timer',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="countdown"
      [class]="'urgency-' + urgency()"
      [attr.aria-live]="ariaLive()"
      [attr.aria-label]="ariaLabel()"
      role="timer"
    >
      <mat-icon aria-hidden="true">timer</mat-icon>
      <span class="time-display">{{ displayTime() }}</span>
      <span class="urgency-label" aria-hidden="true">{{ urgencyLabel() }}</span>
    </div>
  `,
  styles: [
    `
      .countdown {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        padding: 8px 12px;
        border-radius: 8px;
        font-weight: 500;
        font-variant-numeric: tabular-nums;
      }

      /* UXR-112: colour-coded urgency */
      .urgency-green {
        background: #e8f5e9;
        color: #2e7d32;
      }

      .urgency-amber {
        background: #fff3e0;
        color: #e65100;
      }

      .urgency-red {
        background: #ffebee;
        color: #c62828;
        animation: countdown-pulse 1s ease-in-out infinite;
      }

      .urgency-expired {
        background: #f5f5f5;
        color: #9e9e9e;
      }

      @keyframes countdown-pulse {
        0%,
        100% {
          opacity: 1;
        }
        50% {
          opacity: 0.65;
        }
      }

      .urgency-label {
        font-size: 12px;
        opacity: 0.8;
      }
    `,
  ],
})
export class CountdownTimerComponent implements OnInit, OnDestroy {
  @Input({ required: true }) expiresAt!: string;

  private intervalId: ReturnType<typeof setInterval> | null = null;

  readonly remainingMs = signal(0);

  readonly urgency = computed<CountdownUrgency>(() => {
    const ms = this.remainingMs();
    if (ms <= 0) return 'expired';
    if (ms < 30 * 60 * 1_000) return 'red';
    if (ms < 60 * 60 * 1_000) return 'amber';
    return 'green';
  });

  readonly displayTime = computed(() => {
    const ms = this.remainingMs();
    if (ms <= 0) return 'Expired';
    const totalSecs = Math.floor(ms / 1_000);
    const hours = Math.floor(totalSecs / 3_600);
    const minutes = Math.floor((totalSecs % 3_600) / 60);
    const seconds = totalSecs % 60;
    return `${hours}h ${minutes.toString().padStart(2, '0')}m ${seconds.toString().padStart(2, '0')}s`;
  });

  readonly urgencyLabel = computed(() => {
    switch (this.urgency()) {
      case 'expired':
        return 'Claim window expired';
      case 'red':
        return 'Expiring soon!';
      case 'amber':
        return 'Limited time';
      default:
        return 'Time remaining';
    }
  });

  /** UXR-203: assertive when < 30 min so screen readers interrupt immediately. */
  readonly ariaLive = computed(() =>
    this.urgency() === 'red' ? 'assertive' : 'polite',
  );

  readonly ariaLabel = computed(
    () => `Claim window: ${this.displayTime()}. ${this.urgencyLabel()}.`,
  );

  ngOnInit(): void {
    this.tick();
    this.intervalId = setInterval(() => this.tick(), 1_000);
  }

  ngOnDestroy(): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }

  private tick(): void {
    const expiry = new Date(this.expiresAt).getTime();
    this.remainingMs.set(Math.max(0, expiry - Date.now()));
  }
}
