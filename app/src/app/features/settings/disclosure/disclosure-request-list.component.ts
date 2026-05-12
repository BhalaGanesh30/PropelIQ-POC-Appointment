import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  signal,
  effect,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { DisclosureApiService } from './disclosure-api.service';
import { DisclosureRequest, DisclosureStatus } from './models/disclosure.models';

/**
 * Displays the authenticated patient's prior disclosure requests with status
 * tracking and a download action when the report is delivered (US_057, AC-2, AC-3).
 *
 * The parent form component passes a `refreshToken` input that increments after a
 * successful submission to trigger a reload without a full page navigation.
 *
 * Edge case 1: download links are only shown for `Delivered` requests.
 * UXR-303: Switches to card layout below 768 px.
 */
@Component({
  selector: 'app-disclosure-request-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  templateUrl: './disclosure-request-list.component.html',
  styleUrl: './disclosure-request-list.component.scss',
})
export class DisclosureRequestListComponent implements OnInit {
  private readonly api = inject(DisclosureApiService);

  /** Incrementing token from the parent; a new value triggers a reload. */
  readonly refreshToken = input<number>(0);

  readonly requests     = signal<DisclosureRequest[]>([]);
  readonly loading      = signal(false);
  readonly errorMessage = signal('');

  readonly displayedColumns: string[] = ['requestedAt', 'dateRange', 'status', 'actions'];

  constructor() {
    // Reload whenever the parent bumps the refresh token.
    effect(() => {
      this.refreshToken(); // reactive dependency
      this.loadRequests();
    });
  }

  ngOnInit(): void {
    // Initial load handled by the constructor effect.
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  canDownload(request: DisclosureRequest): boolean {
    return request.status === 'Delivered' && request.reportId !== null;
  }

  getDownloadUrl(request: DisclosureRequest): string {
    // Token is delivered via email; this URL is just shown when status=Delivered
    // so the patient can revisit the download. The actual token comes from the email.
    return `/api/v1/patients/me/disclosure-requests/${request.id}/download`;
  }

  statusColor(status: DisclosureStatus): 'primary' | 'accent' | 'warn' | '' {
    switch (status) {
      case 'Delivered': return 'primary';
      case 'Rejected':  return 'warn';
      case 'Approved':
      case 'PendingReview':
      case 'Compiling': return 'accent';
      default: return '';
    }
  }

  // ── Data loading ───────────────────────────────────────────────────────────

  private loadRequests(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.api.list().subscribe({
      next: (items) => {
        this.requests.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Failed to load disclosure requests.');
        this.loading.set(false);
      },
    });
  }
}
