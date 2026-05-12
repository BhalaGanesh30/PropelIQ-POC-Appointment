import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DisclosureApiService } from './disclosure-api.service';
import { DisclosureRequestListComponent } from './disclosure-request-list.component';

/**
 * Patient-facing disclosure request submission form (US_057, AC-2).
 *
 * Route: /settings/disclosure-requests
 * Policy: PatientOnly (enforced via roleGuard on the route)
 *
 * The patient selects a date range and submits the disclosure request.
 * After a successful submission the list component below refreshes.
 * Validation: both dates required; from < to enforced inline.
 *
 * UXR-201: WCAG AA contrast throughout.
 * UXR-202: Keyboard-navigable form controls.
 * UXR-301: Responsive at 375 px / 768 px / 1440 px.
 */
@Component({
  selector: 'app-disclosure-request-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    DisclosureRequestListComponent,
  ],
  templateUrl: './disclosure-request-form.component.html',
  styleUrl: './disclosure-request-form.component.scss',
})
export class DisclosureRequestFormComponent implements OnInit {
  private readonly api      = inject(DisclosureApiService);
  private readonly snackBar = inject(MatSnackBar);

  readonly fromDate     = signal<Date | null>(null);
  readonly toDate       = signal<Date | null>(null);
  readonly submitting   = signal(false);
  /** Increments to trigger the list to reload after a successful submission. */
  readonly refreshToken = signal(0);

  readonly today = new Date();

  /** Inline validation: show error when toDate <= fromDate. */
  get dateRangeValid(): boolean {
    const f = this.fromDate();
    const t = this.toDate();
    return f !== null && t !== null && t > f;
  }

  ngOnInit(): void {
    // Nothing to load on init — list initialises itself.
  }

  submit(): void {
    if (!this.dateRangeValid || this.submitting()) return;

    this.submitting.set(true);

    this.api
      .submit({
        fromDateUtc: this.fromDate()!.toISOString(),
        toDateUtc: this.toDate()!.toISOString(),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          // Reset form.
          this.fromDate.set(null);
          this.toDate.set(null);
          // Trigger list reload.
          this.refreshToken.update((v) => v + 1);
          this.snackBar.open(
            'Disclosure request submitted. You will receive an email when your report is ready.',
            'Dismiss',
            { duration: 6000 },
          );
        },
        error: () => {
          this.submitting.set(false);
          this.snackBar.open(
            'Failed to submit disclosure request. Please try again.',
            'Dismiss',
            { duration: 5000 },
          );
        },
      });
  }
}
