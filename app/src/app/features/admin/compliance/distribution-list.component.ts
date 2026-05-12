import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ComplianceApiService } from './compliance-api.service';
import { DistributionEntry } from './models/compliance.models';

/**
 * Email distribution list management for automated report delivery (US_058, AC-3).
 *
 * Features:
 * - Loads active and inactive recipients.
 * - Inline add form: name + email, validated before submit.
 * - Remove button per recipient.
 * - Toggle active/inactive per recipient.
 */
@Component({
  selector: 'app-distribution-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatProgressBarModule,
    MatSlideToggleModule,
  ],
  templateUrl: './distribution-list.component.html',
})
export class DistributionListComponent implements OnInit {
  private readonly api = inject(ComplianceApiService);

  readonly recipients = signal<DistributionEntry[]>([]);
  readonly loading    = signal(false);
  readonly error      = signal(false);
  readonly saving     = signal(false);

  // ── Add-recipient form ────────────────────────────────────────────────────
  readonly newName  = signal('');
  readonly newEmail = signal('');

  /** Basic RFC 5322 email check — full validation enforced by the API. */
  get isNewEntryValid(): boolean {
    return (
      this.newName().trim().length > 0 &&
      /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.newEmail().trim())
    );
  }

  ngOnInit(): void {
    this.loadRecipients();
  }

  loadRecipients(): void {
    this.loading.set(true);
    this.error.set(false);

    this.api.listRecipients().subscribe({
      next: (list) => {
        this.recipients.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }

  addRecipient(): void {
    if (!this.isNewEntryValid) return;

    this.saving.set(true);
    this.api
      .addRecipient({ name: this.newName().trim(), email: this.newEmail().trim() })
      .subscribe({
        next: (entry) => {
          this.recipients.update((list) => [...list, entry]);
          this.newName.set('');
          this.newEmail.set('');
          this.saving.set(false);
        },
        error: () => this.saving.set(false),
      });
  }

  removeRecipient(entry: DistributionEntry): void {
    this.api.removeRecipient(entry.id).subscribe({
      next: () =>
        this.recipients.update((list) => list.filter((r) => r.id !== entry.id)),
    });
  }

  toggleRecipient(entry: DistributionEntry): void {
    const newValue = !entry.isActive;

    this.api.toggleRecipient(entry.id, newValue).subscribe({
      next: (updated) =>
        this.recipients.update((list) =>
          list.map((r) => (r.id === updated.id ? updated : r)),
        ),
    });
  }
}
