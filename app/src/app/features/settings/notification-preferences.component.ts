import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import {
  ALL_TIMINGS,
  NotificationPreferenceDto,
  REMINDER_TIMING_LABELS,
  ReminderTiming,
} from './models/notification-preference.models';
import { NotificationPreferencesApiService } from './notification-preferences-api.service';

/**
 * Notification Preferences settings page (SCR-009, US_029).
 *
 * AC-1: Patients can toggle Email/SMS channels on or off independently.
 * AC-2: Save button persists changes; next reminder uses the updated settings.
 * Edge case 1: Enabling SMS without a verified phone number reverts the toggle
 *              and shows an inline alert with a link to the profile page.
 *
 * UXR-201: All colour/text combinations meet WCAG AA 4.5:1 contrast.
 * UXR-202: All form controls are keyboard-navigable with visible focus indicators.
 * UXR-301: Responsive at 375px, 768px, 1440px breakpoints.
 * UXR-501: Save button shows spinner and is disabled during the network request.
 * UXR-502: Success toast auto-dismisses at 5 s; error toast persists for user action.
 * UXR-203: Dynamic regions use aria-live for screen readers.
 */
@Component({
  selector: 'app-notification-preferences',
  standalone: true,
  imports: [
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatSnackBarModule,
  ],
  templateUrl: './notification-preferences.component.html',
  styleUrl: './notification-preferences.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationPreferencesComponent implements OnInit, OnDestroy {
  private readonly api = inject(NotificationPreferencesApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly destroy$ = new Subject<void>();

  // ── State signals ──────────────────────────────────────────────────────────
  readonly emailEnabled = signal(true);
  readonly smsEnabled = signal(false);
  readonly reminderTimings = signal<ReminderTiming[]>([...ALL_TIMINGS]);
  readonly hasPhoneNumber = signal(false);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly showPhonePrompt = signal(false);

  // ── Template constants ─────────────────────────────────────────────────────
  readonly allTimings = ALL_TIMINGS;
  readonly timingLabels = REMINDER_TIMING_LABELS;

  /** UXR-501: save button is disabled while any network request is in flight. */
  readonly saveDisabled = computed(() => this.isSaving() || this.isLoading());

  // ── Lifecycle ──────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.loadPreferences();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Event handlers ─────────────────────────────────────────────────────────

  /** AC-1: Toggle email channel. */
  onEmailToggle(enabled: boolean): void {
    this.emailEnabled.set(enabled);
  }

  /**
   * AC-1 + Edge case 1: Toggle SMS with phone number validation guard.
   * If SMS is enabled but no phone is on file, revert the toggle and show
   * the inline prompt instead of persisting an invalid state.
   */
  onSmsToggle(enabled: boolean): void {
    if (enabled && !this.hasPhoneNumber()) {
      this.smsEnabled.set(false);
      this.showPhonePrompt.set(true);
      return;
    }
    this.showPhonePrompt.set(false);
    this.smsEnabled.set(enabled);
  }

  /** Toggle a single reminder timing offset in the selection set. */
  onTimingToggle(timing: ReminderTiming, checked: boolean): void {
    const current = this.reminderTimings();
    this.reminderTimings.set(
      checked ? [...current, timing] : current.filter((t) => t !== timing),
    );
  }

  /** Returns whether a given timing is in the current selection. */
  isTimingEnabled(timing: ReminderTiming): boolean {
    return this.reminderTimings().includes(timing);
  }

  /** Navigate to profile page so the patient can add their phone number. */
  navigateToProfile(): void {
    this.router.navigate(['/profile']);
  }

  /** AC-2: Persist the current preference state. */
  save(): void {
    this.isSaving.set(true);
    const dto: NotificationPreferenceDto = {
      emailEnabled: this.emailEnabled(),
      smsEnabled: this.smsEnabled(),
      reminderTimings: this.reminderTimings(),
    };

    this.api
      .savePreferences(dto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (updated) => {
          this.isSaving.set(false);
          // Sync hasPhoneNumber in case it changed while this page was open.
          this.hasPhoneNumber.set(updated.hasPhoneNumber);
          // UXR-502: success toast auto-dismisses after 5 s.
          this.snackBar.open('Preferences saved', 'Close', { duration: 5000 });
        },
        error: () => {
          this.isSaving.set(false);
          // UXR-502: error toast persists until the user dismisses it.
          this.snackBar.open('Failed to save preferences. Please try again.', 'Retry', {
            duration: 0,
          });
        },
      });
  }

  // ── Private ────────────────────────────────────────────────────────────────

  private loadPreferences(): void {
    this.isLoading.set(true);
    this.api
      .getPreferences()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (prefs) => {
          this.emailEnabled.set(prefs.emailEnabled);
          this.smsEnabled.set(prefs.smsEnabled);
          this.reminderTimings.set([...prefs.reminderTimings] as ReminderTiming[]);
          this.hasPhoneNumber.set(prefs.hasPhoneNumber);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.snackBar.open('Failed to load preferences. Please try again.', 'Retry', {
            duration: 0,
          });
        },
      });
  }
}
