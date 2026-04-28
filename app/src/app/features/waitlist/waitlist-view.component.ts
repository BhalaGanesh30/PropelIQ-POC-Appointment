import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ClaimCountdownComponent } from './claim-countdown.component';
import {
  CountdownTimerComponent,
} from './countdown-timer.component';
import {
  JoinWaitlistDialogComponent,
  JoinWaitlistDialogData,
  JoinWaitlistDialogResult,
} from './join-waitlist-dialog.component';
import { WaitlistApiService } from './waitlist-api.service';
import { WaitlistEntry } from './models/waitlist.models';

/** Auto-refresh interval for the waitlist entries list (30 seconds). */
const REFRESH_INTERVAL_MS = 30_000;

/**
 * Waitlist View page — SCR-008.
 *
 * Five states:
 *   Default     — card list with status chips and countdown timers for offered entries
 *   Loading     — skeleton cards during initial/refresh fetch
 *   Empty       — "Not on any waitlist" illustration with Browse Slots CTA
 *   Error       — retry banner on network failure
 *   Validation  — claim button + urgency countdown; 409 Conflict handled gracefully
 *
 * AC-1: "Join Waitlist" opens JoinWaitlistDialogComponent; calls POST /api/v1/waitlist.
 * AC-3: "Claim Slot" calls POST /api/v1/waitlist/{id}/claim; navigates to
 *       /booking/confirmation/{appointmentId} on success.
 * AC-4: Expired entries show muted treatment with "Claim window expired" label.
 * UXR-112: Countdown timer with green/amber/red/expired urgency colours.
 * UXR-203: aria-live on dynamic status updates.
 * UXR-301: responsive 375px / 768px / 1440px breakpoints.
 */
@Component({
  selector: 'app-waitlist-view',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    ClaimCountdownComponent,
    CountdownTimerComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './waitlist-view.component.html',
  styleUrls: ['./waitlist-view.component.scss'],
})
export class WaitlistViewComponent implements OnInit, OnDestroy {
  private readonly api = inject(WaitlistApiService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  readonly entries = signal<WaitlistEntry[]>([]);
  readonly isLoading = signal(true);
  readonly hasError = signal(false);
  /** Tracks which entry ID has an in-progress claim or cancel action. */
  readonly actionInProgress = signal<string | null>(null);

  readonly activeEntries = computed(() =>
    this.entries().filter((e) => e.status === 'Active'),
  );

  readonly offeredEntries = computed(() =>
    this.entries().filter((e) => e.status === 'Offered'),
  );

  readonly expiredEntries = computed(() =>
    this.entries().filter((e) => e.status === 'Expired'),
  );

  private refreshIntervalId: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.loadEntries();
    this.refreshIntervalId = setInterval(() => this.loadEntries(true), REFRESH_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    if (this.refreshIntervalId !== null) {
      clearInterval(this.refreshIntervalId);
      this.refreshIntervalId = null;
    }
  }

  private loadEntries(silent = false): void {
    if (!silent) this.isLoading.set(true);
    this.hasError.set(false);

    this.api.getEntries().subscribe({
      next: (entries) => {
        this.entries.set(entries);
        this.isLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        console.error('[WaitlistView] Failed to load entries', err);
        this.isLoading.set(false);
        this.hasError.set(true);
      },
    });
  }

  /**
   * Opens the Join Waitlist dialog (AC-1). On confirm, calls the API and
   * prepends the new entry to the list.
   */
  openJoinDialog(): void {
    const ref = this.dialog.open<
      JoinWaitlistDialogComponent,
      JoinWaitlistDialogData,
      JoinWaitlistDialogResult
    >(JoinWaitlistDialogComponent, {
      data: {} as JoinWaitlistDialogData,
      autoFocus: 'first-tabbable',
    });

    ref.afterClosed().subscribe((result) => {
      if (!result) return;

      this.api.joinWaitlist(result).subscribe({
        next: (entry) => {
          this.entries.update((list) => [entry, ...list]);
          this.snackBar.open(
            'You have joined the waitlist. We will notify you when a slot becomes available.',
            'Dismiss',
            { duration: 6_000 },
          );
        },
        error: (err: HttpErrorResponse) => {
          const msg =
            err.status === 409
              ? 'You already have an active entry with the same criteria.'
              : 'Failed to join waitlist. Please try again.';
          this.snackBar.open(msg, 'Dismiss', { duration: 6_000 });
        },
      });
    });
  }

  /**
   * Claims an offered slot (AC-3). On success, navigates to booking confirmation.
   * Handles 409 Conflict when the slot was claimed by another patient first.
   */
  claimSlot(entry: WaitlistEntry): void {
    if (this.actionInProgress() !== null) return;
    this.actionInProgress.set(entry.id);

    this.api.claimSlot(entry.id).subscribe({
      next: (response) => {
        this.actionInProgress.set(null);
        this.entries.update((list) =>
          list.map((e) =>
            e.id === entry.id ? { ...e, status: 'Claimed' as const } : e,
          ),
        );
        this.router.navigate(['/booking/confirmation', response.appointmentId]);
      },
      error: (err: HttpErrorResponse) => {
        this.actionInProgress.set(null);
        const msg =
          err.status === 409
            ? 'This slot was just taken. You have been returned to the waitlist.'
            : 'Failed to claim slot. Please try again.';
        this.snackBar.open(msg, 'Dismiss', { duration: 6_000 });
        if (err.status === 409) {
          // Re-fetch so the entry resets to Active and position is updated.
          this.loadEntries(true);
        }
      },
    });
  }

  /** Cancels (removes) a waitlist entry. */
  cancelEntry(entry: WaitlistEntry): void {
    if (this.actionInProgress() !== null) return;
    this.actionInProgress.set(entry.id);

    this.api.cancelEntry(entry.id).subscribe({
      next: () => {
        this.actionInProgress.set(null);
        this.entries.update((list) => list.filter((e) => e.id !== entry.id));
        this.snackBar.open('Removed from waitlist.', 'Dismiss', { duration: 4_000 });
      },
      error: (err: HttpErrorResponse) => {
        this.actionInProgress.set(null);
        console.error('[WaitlistView] Failed to cancel entry', err);
        this.snackBar.open('Failed to remove entry. Please try again.', 'Dismiss', {
          duration: 6_000,
        });
      },
    });
  }

  navigateToSearch(): void {
    this.router.navigate(['/scheduling/search']);
  }

  retry(): void {
    this.loadEntries();
  }

  /** Returns the chip colour for a waitlist status. */
  statusColor(status: WaitlistEntry['status']): string {
    switch (status) {
      case 'Offered':
        return 'accent';
      case 'Expired':
      case 'Cancelled':
        return '';
      default:
        return 'primary';
    }
  }
}
