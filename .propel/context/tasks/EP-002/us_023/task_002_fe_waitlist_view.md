# Task - TASK_002

## Requirement Reference

- User Story: us_023
- Story Location: .propel/context/tasks/EP-002/us_023/us_023.md
- Acceptance Criteria:
  - AC-1: Given no slots match my search criteria, When I click "Join Waitlist," Then my preferred slot parameters (date range, duration, type) are saved and I receive a confirmation that I am on the waitlist.
  - AC-2: Given a matching slot becomes available (due to cancellation or release), When the system identifies eligible waitlisted patients, Then the first eligible patient receives a preferred-slot alert notification within 5 minutes of the slot becoming available.
  - AC-3: Given I receive a preferred-slot alert, When I claim the slot within 2 hours, Then the slot is reserved for me and I receive the standard booking confirmation artifacts.
  - AC-4: Given I do not claim the slot within 2 hours, When the claim window expires, Then the slot is released and the next eligible waitlisted patient is notified.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-008-waitlist.html` (pending) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-008 |
| **UXR Requirements** | UXR-112 (countdown timer with urgency color shift), UXR-201 (typography), UXR-203 (screen reader announcements for dynamic content), UXR-301 (responsive breakpoints) |
| **Design Tokens** | N/A |

> **Note**: User story references SCR-010 but the actual Waitlist screen is **SCR-008** per figma_spec.md. SCR-010 does not exist in figma_spec.md.

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Frontend | Angular Material | 17.x |
| Frontend | RxJS | 7.x |
| Frontend | TypeScript | 5.x |
| Backend | N/A | N/A |
| Database | N/A | N/A |
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

Implement the Waitlist View page (SCR-008) displaying active waitlist entries with preferred slot criteria, claim countdown timers for offered slots, and a "Claim" button. The page renders a card-based list per SCR-008 specification with five states (Default, Loading, Empty, Error, Validation). Each offered-slot card includes a real-time countdown timer with color-coded urgency per UXR-112: green (>1h remaining), amber (30m–1h), red (<30m). The "Join Waitlist" action is triggered from the slot search empty state (SCR-004 per US_019 task_002) and calls the `POST /api/v1/waitlist` endpoint (AC-1). The "Claim" button on offered entries calls `POST /api/v1/waitlist/{id}/claim` and navigates to the booking confirmation page on success (AC-3). When a claim window expires, the entry card transitions to an "Expired" state with visual treatment (AC-4). Screen reader announcements are provided for timer updates and status changes per UXR-203. The page supports 3 responsive breakpoints (UXR-301): mobile 375px, tablet 768px, desktop 1440px.

## Dependent Tasks

- US_023 task_001 (requires POST /api/v1/waitlist, GET /api/v1/waitlist, POST /api/v1/waitlist/{id}/claim endpoints)
- US_019 task_002 (requires slot search page with "Join Waitlist" CTA on empty state)
- US_021 task_003 (requires booking confirmation page for post-claim navigation)

## Impacted Components

- New: `client/src/app/features/waitlist/waitlist-view.component.ts` (standalone component)
- New: `client/src/app/features/waitlist/waitlist-view.component.html` (template)
- New: `client/src/app/features/waitlist/waitlist-view.component.scss` (responsive styles)
- New: `client/src/app/features/waitlist/countdown-timer.component.ts` (reusable countdown with urgency)
- New: `client/src/app/features/waitlist/waitlist-api.service.ts` (API service)
- New: `client/src/app/features/waitlist/models/waitlist.models.ts` (TypeScript interfaces)
- New: `client/src/app/features/waitlist/join-waitlist-dialog.component.ts` (join preferences dialog)
- Modify: `client/src/app/app.routes.ts` (add waitlist route)

## Implementation Plan

1. **Create TypeScript interfaces**:

```typescript
// client/src/app/features/waitlist/models/waitlist.models.ts

export interface JoinWaitlistRequest {
  preferredDateStart: string;
  preferredDateEnd: string;
  preferredDurationMinutes: number;
  preferredAppointmentType: string;
}

export interface WaitlistEntry {
  id: string;
  status: 'Active' | 'Offered' | 'Claimed' | 'Expired' | 'Cancelled';
  preferredDateStart: string;
  preferredDateEnd: string;
  preferredDurationMinutes: number;
  preferredAppointmentType: string;
  offeredSlotId: string | null;
  offeredAt: string | null;
  claimExpiresAt: string | null;
  position: number;
  createdAt: string;
}

export interface ClaimResponse {
  appointmentId: string;
  confirmationCode: string;
  appointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
}

export type CountdownUrgency = 'green' | 'amber' | 'red' | 'expired';
```

2. **Create `WaitlistApiService`**:

```typescript
// client/src/app/features/waitlist/waitlist-api.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  JoinWaitlistRequest,
  WaitlistEntry,
  ClaimResponse
} from './models/waitlist.models';

@Injectable({ providedIn: 'root' })
export class WaitlistApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/waitlist';

  joinWaitlist(request: JoinWaitlistRequest): Observable<WaitlistEntry> {
    return this.http.post<WaitlistEntry>(this.baseUrl, request);
  }

  getEntries(): Observable<WaitlistEntry[]> {
    return this.http.get<WaitlistEntry[]>(this.baseUrl);
  }

  claimSlot(entryId: string): Observable<ClaimResponse> {
    return this.http.post<ClaimResponse>(
      `${this.baseUrl}/${entryId}/claim`, {}
    );
  }

  cancelEntry(entryId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${entryId}`);
  }
}
```

3. **Create `CountdownTimerComponent`** with UXR-112 urgency colors:

```typescript
// client/src/app/features/waitlist/countdown-timer.component.ts
import {
  Component, Input, signal, OnInit, OnDestroy,
  computed, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { CountdownUrgency } from './models/waitlist.models';

@Component({
  selector: 'app-countdown-timer',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="countdown"
         [class]="'urgency-' + urgency()"
         [attr.aria-live]="ariaLive()"
         [attr.aria-label]="ariaLabel()"
         role="timer">
      <mat-icon>timer</mat-icon>
      <span class="time-display">{{ displayTime() }}</span>
      <span class="urgency-label">{{ urgencyLabel() }}</span>
    </div>
  `,
  styles: [`
    .countdown {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 12px;
      border-radius: 8px;
      font-weight: 500;
      font-variant-numeric: tabular-nums;
    }
    // UXR-112: color-coded urgency
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
      animation: pulse 1s ease-in-out infinite;
    }
    .urgency-expired {
      background: #f5f5f5;
      color: #9e9e9e;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.7; }
    }
    .urgency-label {
      font-size: 12px;
      opacity: 0.8;
    }
  `]
})
export class CountdownTimerComponent implements OnInit, OnDestroy {
  @Input({ required: true }) expiresAt!: string;

  private intervalId: ReturnType<typeof setInterval> | null = null;
  readonly remainingMs = signal(0);

  readonly urgency = computed<CountdownUrgency>(() => {
    const ms = this.remainingMs();
    if (ms <= 0) return 'expired';
    if (ms <= 30 * 60 * 1000) return 'red';       // < 30 min
    if (ms <= 60 * 60 * 1000) return 'amber';      // 30m – 1h
    return 'green';                                  // > 1h
  });

  readonly displayTime = computed(() => {
    const ms = this.remainingMs();
    if (ms <= 0) return 'Expired';
    const totalSecs = Math.floor(ms / 1000);
    const hours = Math.floor(totalSecs / 3600);
    const minutes = Math.floor((totalSecs % 3600) / 60);
    const seconds = totalSecs % 60;
    return `${hours}h ${minutes.toString().padStart(2, '0')}m ${seconds.toString().padStart(2, '0')}s`;
  });

  readonly urgencyLabel = computed(() => {
    const u = this.urgency();
    if (u === 'expired') return 'Claim window expired';
    if (u === 'red') return 'Expiring soon!';
    if (u === 'amber') return 'Limited time';
    return 'Time remaining';
  });

  // UXR-203: Screen reader announcements for dynamic content
  readonly ariaLive = computed(() =>
    this.urgency() === 'red' ? 'assertive' : 'polite'
  );

  readonly ariaLabel = computed(() =>
    `Claim window: ${this.displayTime()}. ${this.urgencyLabel()}.`
  );

  ngOnInit(): void {
    this.updateRemaining();
    this.intervalId = setInterval(() => this.updateRemaining(), 1000);
  }

  ngOnDestroy(): void {
    if (this.intervalId) clearInterval(this.intervalId);
  }

  private updateRemaining(): void {
    const expiry = new Date(this.expiresAt).getTime();
    this.remainingMs.set(Math.max(0, expiry - Date.now()));
  }
}
```

4. **Create `JoinWaitlistDialogComponent`** for preferences capture:

```typescript
// client/src/app/features/waitlist/join-waitlist-dialog.component.ts
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  MatDialogModule,
  MAT_DIALOG_DATA,
  MatDialogRef
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatIconModule } from '@angular/material/icon';
import { JoinWaitlistRequest } from './models/waitlist.models';

export interface JoinDialogData {
  appointmentType?: string;
  duration?: number;
}

@Component({
  selector: 'app-join-waitlist-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatDatepickerModule, MatIconModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon>playlist_add</mat-icon>
      Join Waitlist
    </h2>
    <mat-dialog-content>
      <p>Set your preferred slot criteria. You'll be notified when a matching
         slot becomes available.</p>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Appointment Type</mat-label>
        <mat-select [(ngModel)]="appointmentType" required
                    aria-label="Preferred appointment type">
          <mat-option value="General">General</mat-option>
          <mat-option value="Specialist">Specialist</mat-option>
          <mat-option value="FollowUp">Follow-Up</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Duration</mat-label>
        <mat-select [(ngModel)]="duration" required
                    aria-label="Preferred duration">
          <mat-option [value]="15">15 minutes</mat-option>
          <mat-option [value]="30">30 minutes</mat-option>
          <mat-option [value]="60">60 minutes</mat-option>
        </mat-select>
      </mat-form-field>

      <div class="date-range">
        <mat-form-field appearance="outline">
          <mat-label>From Date</mat-label>
          <input matInput [matDatepicker]="startPicker"
                 [(ngModel)]="dateStart" required
                 aria-label="Preferred start date">
          <mat-datepicker-toggle matIconSuffix [for]="startPicker">
          </mat-datepicker-toggle>
          <mat-datepicker #startPicker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>To Date</mat-label>
          <input matInput [matDatepicker]="endPicker"
                 [(ngModel)]="dateEnd" required
                 aria-label="Preferred end date">
          <mat-datepicker-toggle matIconSuffix [for]="endPicker">
          </mat-datepicker-toggle>
          <mat-datepicker #endPicker></mat-datepicker>
        </mat-form-field>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close
              aria-label="Cancel join waitlist">
        Cancel
      </button>
      <button mat-flat-button color="primary"
              [disabled]="!isValid()"
              (click)="submit()"
              aria-label="Join waitlist">
        <mat-icon>check</mat-icon> Join Waitlist
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; margin-bottom: 8px; }
    .date-range {
      display: flex;
      gap: 16px;
      mat-form-field { flex: 1; }
    }
  `]
})
export class JoinWaitlistDialogComponent {
  private readonly data = inject<JoinDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(
    MatDialogRef<JoinWaitlistDialogComponent>
  );

  appointmentType = this.data.appointmentType ?? '';
  duration = this.data.duration ?? 30;
  dateStart = '';
  dateEnd = '';

  isValid(): boolean {
    return !!(this.appointmentType && this.duration
           && this.dateStart && this.dateEnd);
  }

  submit(): void {
    const result: JoinWaitlistRequest = {
      preferredAppointmentType: this.appointmentType,
      preferredDurationMinutes: this.duration,
      preferredDateStart: this.dateStart,
      preferredDateEnd: this.dateEnd
    };
    this.dialogRef.close(result);
  }
}
```

5. **Create `WaitlistViewComponent`**:

```typescript
// client/src/app/features/waitlist/waitlist-view.component.ts
import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { WaitlistApiService } from './waitlist-api.service';
import { WaitlistEntry } from './models/waitlist.models';
import { CountdownTimerComponent } from './countdown-timer.component';
import {
  JoinWaitlistDialogComponent,
  JoinDialogData
} from './join-waitlist-dialog.component';

@Component({
  selector: 'app-waitlist-view',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    CountdownTimerComponent
  ],
  templateUrl: './waitlist-view.component.html',
  styleUrl: './waitlist-view.component.scss'
})
export class WaitlistViewComponent implements OnInit, OnDestroy {
  private readonly waitlistApi = inject(WaitlistApiService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly entries = signal<WaitlistEntry[]>([]);
  readonly isLoading = signal(true);
  readonly claimingEntryId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  private refreshInterval: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.loadEntries();
    // Refresh every 30 seconds to catch status transitions
    this.refreshInterval = setInterval(() => this.loadEntries(), 30_000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
  }

  loadEntries(): void {
    this.waitlistApi.getEntries().subscribe({
      next: (entries) => {
        this.entries.set(entries);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Failed to load waitlist entries.');
      }
    });
  }

  // AC-3: Claim offered slot
  claimSlot(entry: WaitlistEntry): void {
    if (this.claimingEntryId()) return;

    this.claimingEntryId.set(entry.id);

    this.waitlistApi.claimSlot(entry.id).subscribe({
      next: (response) => {
        this.claimingEntryId.set(null);
        this.snackBar.open(
          'Slot claimed! Booking confirmed.', 'Close',
          { duration: 4000 }
        );
        // Navigate to booking confirmation (standard artifacts)
        this.router.navigate(
          ['/booking/confirmation', response.appointmentId],
          { state: { booking: response } }
        );
      },
      error: (err) => {
        this.claimingEntryId.set(null);
        const msg = err.error?.message ?? 'Failed to claim slot.';
        this.snackBar.open(msg, 'Close', { duration: 5000 });
        // Refresh entries to reflect current state
        this.loadEntries();
      }
    });
  }

  // Open join dialog (triggered from slot search empty state)
  openJoinDialog(data?: JoinDialogData): void {
    const dialogRef = this.dialog.open(JoinWaitlistDialogComponent, {
      data: data ?? {},
      width: '500px'
    });

    dialogRef.afterClosed().subscribe((request) => {
      if (!request) return;

      this.waitlistApi.joinWaitlist(request).subscribe({
        next: () => {
          this.snackBar.open(
            'You are now on the waitlist!', 'Close',
            { duration: 4000 }
          );
          this.loadEntries();
        },
        error: () => {
          this.snackBar.open(
            'Failed to join waitlist.', 'Close',
            { duration: 5000 }
          );
        }
      });
    });
  }

  cancelEntry(entry: WaitlistEntry): void {
    this.waitlistApi.cancelEntry(entry.id).subscribe({
      next: () => {
        this.snackBar.open(
          'Removed from waitlist.', 'Close',
          { duration: 3000 }
        );
        this.entries.update(list =>
          list.filter(e => e.id !== entry.id)
        );
      },
      error: () => {
        this.snackBar.open(
          'Failed to remove from waitlist.', 'Close',
          { duration: 5000 }
        );
      }
    });
  }

  navigateToSearch(): void {
    this.router.navigate(['/appointments/search']);
  }

  retryLoad(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.loadEntries();
  }

  isExpired(entry: WaitlistEntry): boolean {
    if (entry.status === 'Expired') return true;
    if (entry.claimExpiresAt) {
      return new Date(entry.claimExpiresAt).getTime() <= Date.now();
    }
    return false;
  }
}
```

6. **Create template**:

```html
<!-- client/src/app/features/waitlist/waitlist-view.component.html -->

<div class="waitlist-container">
  <h1>My Waitlist</h1>

  <!-- Loading state (SCR-008 Loading) -->
  @if (isLoading()) {
    <div class="skeleton-cards">
      @for (i of [1, 2, 3]; track i) {
        <div class="skeleton-card"></div>
      }
    </div>
  }

  <!-- Error state (SCR-008 Error) -->
  @if (errorMessage(); as msg) {
    <div class="error-banner" role="alert">
      <mat-icon color="warn">error_outline</mat-icon>
      <span>{{ msg }}</span>
      <button mat-stroked-button (click)="retryLoad()"
              aria-label="Retry loading waitlist">
        Retry
      </button>
    </div>
  }

  <!-- Empty state (SCR-008 Empty) -->
  @if (!isLoading() && !errorMessage() && entries().length === 0) {
    <div class="empty-state">
      <mat-icon class="empty-icon">hourglass_empty</mat-icon>
      <p>Not on any waitlist</p>
      <button mat-flat-button color="primary"
              (click)="navigateToSearch()"
              aria-label="Browse available slots">
        Browse Slots
      </button>
    </div>
  }

  <!-- Waitlist entries (SCR-008 Default + Validation states) -->
  @if (!isLoading() && entries().length > 0) {
    <div class="entries-list"
         role="list"
         aria-label="Waitlist entries">

      @for (entry of entries(); track entry.id) {
        <mat-card class="waitlist-card"
                  [class.offered]="entry.status === 'Offered' && !isExpired(entry)"
                  [class.expired]="isExpired(entry)"
                  role="listitem">
          <mat-card-header>
            <mat-card-title>
              {{ entry.preferredAppointmentType }}
              — {{ entry.preferredDurationMinutes }} min
            </mat-card-title>
            <mat-card-subtitle>
              {{ entry.preferredDateStart | date:'MMM d' }}
              – {{ entry.preferredDateEnd | date:'MMM d, y' }}
            </mat-card-subtitle>
            <mat-chip [class]="'status-' + entry.status.toLowerCase()">
              {{ entry.status }}
            </mat-chip>
          </mat-card-header>

          <mat-card-content>
            <div class="entry-details">
              <div class="detail-item">
                <mat-icon>sort</mat-icon>
                <span>Position: #{{ entry.position }}</span>
              </div>
              <div class="detail-item">
                <mat-icon>event</mat-icon>
                <span>Joined: {{ entry.createdAt | date:'MMM d, y' }}</span>
              </div>
            </div>

            <!-- Countdown timer for offered slots (UXR-112) -->
            @if (entry.status === 'Offered' && entry.claimExpiresAt) {
              <app-countdown-timer
                [expiresAt]="entry.claimExpiresAt">
              </app-countdown-timer>
            }
          </mat-card-content>

          <mat-card-actions>
            <!-- AC-3: Claim button for offered slots (SCR-008 Validation) -->
            @if (entry.status === 'Offered' && !isExpired(entry)) {
              <button mat-flat-button color="primary"
                      [disabled]="claimingEntryId() !== null"
                      (click)="claimSlot(entry)"
                      aria-label="Claim offered slot">
                @if (claimingEntryId() === entry.id) {
                  <mat-spinner diameter="20"></mat-spinner>
                } @else {
                  <mat-icon>check_circle</mat-icon>
                }
                Claim Slot
              </button>
            }

            <!-- Remove from waitlist -->
            @if (entry.status === 'Active') {
              <button mat-stroked-button color="warn"
                      (click)="cancelEntry(entry)"
                      aria-label="Remove from waitlist">
                <mat-icon>close</mat-icon> Remove
              </button>
            }

            <!-- Expired entry message -->
            @if (isExpired(entry)) {
              <span class="expired-label"
                    role="status"
                    aria-live="polite">
                <mat-icon>timer_off</mat-icon>
                Claim window expired — waiting for next slot
              </span>
            }
          </mat-card-actions>
        </mat-card>
      }
    </div>
  }
</div>
```

7. **Create styles** with responsive breakpoints and urgency colors:

```scss
// client/src/app/features/waitlist/waitlist-view.component.scss

.waitlist-container {
  padding: 24px 16px;
  max-width: 900px;
  margin: 0 auto;

  h1 {
    margin-bottom: 24px;
  }
}

// Skeleton loading (SCR-008 Loading)
.skeleton-cards {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.skeleton-card {
  height: 140px;
  background: linear-gradient(
    90deg,
    var(--mat-sys-surface-variant) 25%,
    var(--mat-sys-surface) 50%,
    var(--mat-sys-surface-variant) 75%
  );
  background-size: 200% 100%;
  animation: shimmer 1.5s infinite;
  border-radius: 12px;
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

// Error banner (SCR-008 Error)
.error-banner {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  background: var(--mat-sys-error-container);
  border-radius: 8px;
  margin-bottom: 24px;
}

// Empty state (SCR-008 Empty)
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 64px 16px;
  text-align: center;

  .empty-icon {
    font-size: 64px;
    height: 64px;
    width: 64px;
    color: var(--mat-sys-on-surface-variant);
    margin-bottom: 16px;
  }
}

// Card-based list (SCR-008 layout)
.entries-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.waitlist-card {
  border-radius: 12px; // UXR-403: 8px border-radius (card elevated to 12)
  transition: border-color 0.3s ease;

  // Offered card emphasis
  &.offered {
    border-left: 4px solid #2e7d32;
  }

  // Expired card de-emphasis
  &.expired {
    opacity: 0.6;
    border-left: 4px solid #9e9e9e;
  }

  mat-card-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: 8px;
  }

  mat-card-actions {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 8px 16px;
  }
}

.entry-details {
  display: flex;
  gap: 24px;
  padding: 8px 0;
}

.detail-item {
  display: flex;
  align-items: center;
  gap: 6px;
  color: var(--mat-sys-on-surface-variant);

  mat-icon {
    font-size: 18px;
    height: 18px;
    width: 18px;
  }
}

.expired-label {
  display: flex;
  align-items: center;
  gap: 6px;
  color: var(--mat-sys-on-surface-variant);
  font-style: italic;
}

// Status chips
.status-active {
  background: var(--mat-sys-primary-container) !important;
  color: var(--mat-sys-on-primary-container) !important;
}

.status-offered {
  background: #e8f5e9 !important;
  color: #2e7d32 !important;
}

.status-claimed {
  background: var(--mat-sys-tertiary-container) !important;
  color: var(--mat-sys-on-tertiary-container) !important;
}

.status-expired {
  background: var(--mat-sys-surface-variant) !important;
  color: var(--mat-sys-on-surface-variant) !important;
}

// UXR-301: Responsive breakpoints
// Mobile (375px)
@media (max-width: 599px) {
  .waitlist-container {
    padding: 16px 12px;
  }

  .entry-details {
    flex-direction: column;
    gap: 8px;
  }

  mat-card-header {
    flex-direction: column;
    align-items: flex-start !important;
  }
}

// Tablet (768px)
@media (min-width: 600px) and (max-width: 1023px) {
  .waitlist-container {
    padding: 24px;
  }
}

// Desktop (1440px)
@media (min-width: 1024px) {
  .waitlist-container {
    padding: 32px;
  }
}
```

8. **Add route** to application routing:

```typescript
// Add to client/src/app/app.routes.ts
{
  path: 'waitlist',
  loadComponent: () =>
    import('./features/waitlist/waitlist-view.component')
      .then(m => m.WaitlistViewComponent),
  canActivate: [authGuard]
}
```

## Current Project State

```text
propelIQ/
└── client/
    └── src/
        └── app/
            ├── features/
            │   ├── scheduling/                     (existing from US_019)
            │   ├── intake/                         (existing from US_020)
            │   ├── booking/                        (existing from US_021)
            │   ├── appointments/                   (existing from US_022)
            │   └── waitlist/                       (new module)
            │       ├── waitlist-view.component.ts
            │       ├── waitlist-view.component.html
            │       ├── waitlist-view.component.scss
            │       ├── countdown-timer.component.ts
            │       ├── join-waitlist-dialog.component.ts
            │       ├── waitlist-api.service.ts
            │       └── models/
            │           └── waitlist.models.ts
            └── app.routes.ts                       (modify — add waitlist route)
```

> Placeholder: Update on execution based on US_023 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/src/app/features/waitlist/models/waitlist.models.ts | TypeScript interfaces for waitlist entry, join request, claim response, urgency |
| CREATE | client/src/app/features/waitlist/waitlist-api.service.ts | HttpClient service for join, get entries, claim, cancel |
| CREATE | client/src/app/features/waitlist/countdown-timer.component.ts | Reusable countdown with UXR-112 urgency colors (green/amber/red) and aria-live |
| CREATE | client/src/app/features/waitlist/join-waitlist-dialog.component.ts | Material dialog for capturing preferred slot parameters |
| CREATE | client/src/app/features/waitlist/waitlist-view.component.ts | Standalone component with entry list, claim action, 30s auto-refresh |
| CREATE | client/src/app/features/waitlist/waitlist-view.component.html | Template with all 5 SCR-008 states, countdown timers, claim buttons |
| CREATE | client/src/app/features/waitlist/waitlist-view.component.scss | Responsive styles with urgency colors, card emphasis, skeleton loading |
| MODIFY | client/src/app/app.routes.ts | Add lazy-loaded waitlist route with auth guard |

## External References

- Angular Signals: https://angular.dev/guide/signals
- Angular Material Card: https://material.angular.io/components/card/overview
- Angular Material Dialog: https://material.angular.io/components/dialog/overview
- WAI-ARIA live regions: https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/ARIA_Live_Regions

## Build Commands

```bash
# Build frontend
cd client
ng build

# Serve locally
ng serve

# Navigate to: http://localhost:4200/waitlist
```

## Implementation Validation Strategy

- [ ] Waitlist entries display with preferred slot criteria and position number (SCR-008 Default)
- [ ] Skeleton loading cards render during data fetch (SCR-008 Loading)
- [ ] Empty state with "Not on any waitlist" and "Browse Slots" CTA (SCR-008 Empty)
- [ ] Error retry banner appears on load failure (SCR-008 Error)
- [ ] Countdown timer displays with real-time updates every second (UXR-112)
- [ ] Timer shows green (>1h), amber (30m–1h), red with pulse (<30m) urgency colors (UXR-112)
- [ ] Screen reader `aria-live` announcements on timer state changes (UXR-203)
- [ ] "Claim Slot" button triggers API call and navigates to confirmation on success (AC-3)
- [ ] Concurrent claim failure shows snackbar and refreshes entries (edge case)
- [ ] Expired entries show "Claim window expired" label with muted visual treatment (AC-4)
- [ ] 30-second auto-refresh keeps entries current with server state
- [ ] Responsive layout at 375px, 768px, 1440px breakpoints (UXR-301)
- [ ] `aria-label` on all interactive elements and `role="timer"` on countdown (NFR-009)

## Implementation Checklist

- [x] Create waitlist entry, join request, claim response TypeScript interfaces
- [x] Create `WaitlistApiService` with join, getEntries, claim, cancel methods
- [x] Create `CountdownTimerComponent` with 1-second interval, urgency color computation, and aria-live
- [x] Create `JoinWaitlistDialogComponent` for preferred slot parameter capture
- [x] Create `WaitlistViewComponent` with all 5 SCR-008 states and 30s auto-refresh
- [x] Create template with card list, countdown timers, claim buttons, and expired labels
- [x] Create SCSS with urgency colors, card emphasis, skeleton loading, responsive breakpoints
- [x] Add lazy-loaded waitlist route to `app.routes.ts` with auth guard
