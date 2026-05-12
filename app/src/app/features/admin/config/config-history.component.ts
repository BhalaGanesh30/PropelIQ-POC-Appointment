import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { DatePipe, JsonPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { ConfigApiService } from './config-api.service';
import { ConfigCategory, ConfigVersion } from './models/config.models';

/**
 * Version history table for a single configuration category (US_059, AC-3, AC-4).
 *
 * States:
 * - Loading: progress bar.
 * - Empty: "No history yet" message.
 * - Default: mat-table with version, date, admin, restored indicator, and action buttons.
 * - Expanded diff row: before/after JSON panels (AC-3).
 *
 * Emits `restored` when a version is successfully restored so the parent can
 * reload the category form (AC-4).
 */
@Component({
  selector: 'app-config-history',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    JsonPipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTableModule,
  ],
  templateUrl: './config-history.component.html',
})
export class ConfigHistoryComponent implements OnInit {
  readonly category = input.required<ConfigCategory>();

  /** Fires after a successful restore operation. */
  readonly restored = output<void>();

  private readonly api      = inject(ConfigApiService);
  private readonly snackBar = inject(MatSnackBar);

  readonly versions     = signal<ConfigVersion[]>([]);
  readonly loading      = signal(false);
  readonly expandedId   = signal<string | null>(null);

  readonly displayedColumns = ['version', 'changedAt', 'changedBy', 'restoredFrom', 'actions'];

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loading.set(true);
    this.api.getHistory(this.category()).subscribe({
      next: (data) => {
        this.versions.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.snackBar.open('Failed to load version history.', 'Dismiss', { duration: 5000 });
        this.loading.set(false);
      },
    });
  }

  toggleDiff(versionId: string): void {
    this.expandedId.set(this.expandedId() === versionId ? null : versionId);
  }

  restore(version: ConfigVersion): void {
    this.api.restore(this.category(), version.versionId).subscribe({
      next: (result) => {
        this.snackBar.open(
          `Restored as version ${result.versionNumber}`,
          'Dismiss',
          { duration: 4000 },
        );
        this.loadHistory();
        this.restored.emit();
      },
      error: () =>
        this.snackBar.open('Restore failed — please try again.', 'Dismiss', {
          duration: 5000,
        }),
    });
  }
}
