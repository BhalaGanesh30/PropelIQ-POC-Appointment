import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ClaimCountdownComponent } from './claim-countdown.component';
import { SlotClaimApiService } from './slot-claim-api.service';
import { ClaimResult, SlotClaimDetails } from './models/slot-claim.models';

/**
 * Slot claim page — SCR-008 claim flow (US_030 task_002).
 *
 * Opened when the patient follows the HMAC-signed claim link from their slot
 * alert email or SMS. Resolves the link token to slot details, shows the
 * live countdown timer (AC-2), and handles the "Claim Appointment" action (AC-3).
 *
 * States:
 *   loading     — spinner while resolving the claim token
 *   claim-form  — slot details + countdown + Claim button
 *   claimed     — success confirmation with dashboard link
 *   expired     — 410/0 countdown state with "Return to Waitlist" link (AC-4)
 *   error       — unexpected failure
 *
 * UXR-203: role="timer" and aria-live announcements delegated to ClaimCountdownComponent.
 * UXR-301: responsive across 375px, 768px, 1440px.
 * UXR-201: WCAG AA contrast on all urgency colors.
 */
@Component({
  selector: 'app-slot-claim-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    ClaimCountdownComponent,
  ],
  templateUrl: './slot-claim-page.component.html',
  styleUrl: './slot-claim-page.component.scss',
})
export class SlotClaimPageComponent implements OnInit {
  private readonly route  = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api    = inject(SlotClaimApiService);

  readonly slot          = signal<SlotClaimDetails | null>(null);
  readonly claimResult   = signal<ClaimResult | null>(null);
  readonly loading       = signal(true);
  readonly claiming      = signal(false);
  readonly claimed       = signal(false);
  readonly expired       = signal(false);
  readonly errorMessage  = signal('');

  /** True once slot details loaded and window has not expired. */
  readonly showClaimForm = computed(
    () => !this.loading() && !this.claimed() && !this.expired() && !this.errorMessage() && !!this.slot(),
  );

  private token = '';

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';

    if (!this.token) {
      this.errorMessage.set('Invalid claim link. No token was provided.');
      this.loading.set(false);
      return;
    }

    this.api.getClaimDetails(this.token).subscribe({
      next: (details) => {
        this.slot.set(details);

        if (details.status === 'Expired') {
          this.expired.set(true);
          this.errorMessage.set('This claim window has already expired.');
        } else if (details.status === 'Claimed') {
          this.claimed.set(true);
        }

        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        if (err.status === 410) {
          this.expired.set(true);
          this.errorMessage.set(
            'This slot has been offered to another patient. Please check your waitlist for new offers.',
          );
        } else if (err.status === 400) {
          this.errorMessage.set('Invalid claim link. The link may have been altered or expired.');
        } else if (err.status === 404) {
          this.errorMessage.set('Claim details not found. The link may be outdated.');
        } else {
          this.errorMessage.set('Unable to load claim details. Please try again later.');
        }
      },
    });
  }

  /**
   * Claim the offered slot (AC-3).
   * Sends the HMAC token to the backend for server-side verification.
   */
  claim(): void {
    const s = this.slot();
    if (!s || this.claiming() || this.expired()) return;

    this.claiming.set(true);
    this.errorMessage.set('');

    this.api.claimSlot(s.waitlistEntryId, this.token).subscribe({
      next: (result) => {
        this.claimResult.set(result);
        this.claiming.set(false);
        this.claimed.set(true);
      },
      error: (err: HttpErrorResponse) => {
        this.claiming.set(false);
        if (err.status === 410) {
          // AC-4: Claim window expired between page load and button click.
          this.expired.set(true);
          this.errorMessage.set(
            'This offer has expired. The slot has been offered to the next patient on the waitlist.',
          );
        } else if (err.status === 409) {
          this.errorMessage.set(
            'This slot was just claimed by another patient. You remain on the waitlist for a new offer.',
          );
        } else if (err.status === 400) {
          this.errorMessage.set('Claim failed: invalid or expired claim token.');
        } else {
          this.errorMessage.set('Failed to claim the slot. Please try again.');
        }
      },
    });
  }

  /** Called by the countdown when the timer reaches zero. */
  onCountdownExpired(): void {
    if (!this.claimed()) {
      this.expired.set(true);
      this.errorMessage.set(
        'The claim window has closed. The slot has been offered to the next patient.',
      );
    }
  }

  navigateToDashboard(): void {
    void this.router.navigate(['/dashboard']);
  }

  navigateToWaitlist(): void {
    void this.router.navigate(['/waitlist']);
  }
}
