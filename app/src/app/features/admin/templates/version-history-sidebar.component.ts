import { Component, inject, input, OnChanges, output, signal } from '@angular/core';
import { ChangeDetectionStrategy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TemplateApiService } from './template-api.service';
import { RestoreConfirmDialogComponent } from './restore-confirm-dialog.component';
import { TemplateVersionItem } from './models/template.models';

/**
 * Collapsible version history sidebar for SCR-024 (US_062, AC-1, AC-3).
 *
 * Lists all versions in reverse chronological order (newest first).
 * Each non-active version has a Restore button that opens a confirmation
 * dialog before calling the restore endpoint (AC-3).
 *
 * Reloads automatically when `templateId` input changes (OnChanges).
 */
@Component({
  selector: 'app-version-history-sidebar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, MatIconModule, MatListModule, MatProgressBarModule],
  templateUrl: './version-history-sidebar.component.html',
  styleUrl: './version-history-sidebar.component.scss',
})
export class VersionHistorySidebarComponent implements OnChanges {
  readonly templateId = input.required<string>();
  readonly versionRestored = output<void>();

  private readonly api = inject(TemplateApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly versions = signal<TemplateVersionItem[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal(false);

  ngOnChanges(): void {
    this.loadVersions();
  }

  loadVersions(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.api.getVersions(this.templateId()).subscribe({
      next: (data) => {
        this.versions.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  restore(version: TemplateVersionItem): void {
    const dialogRef = this.dialog.open(RestoreConfirmDialogComponent, {
      data: { versionNumber: version.versionNumber },
      width: '420px',
    });

    dialogRef.afterClosed().subscribe((confirmed: boolean | undefined) => {
      if (!confirmed) return;

      this.api.restore(this.templateId(), version.id).subscribe({
        next: (newVersion) => {
          this.snackBar.open(
            `Restored as version ${newVersion.versionNumber}`,
            'Dismiss',
            { duration: 5000 },
          );
          this.versionRestored.emit();
          this.loadVersions();
        },
        error: () => {
          this.snackBar.open('Restore failed. Please try again.', 'Dismiss', {
            duration: 0,
          });
        },
      });
    });
  }
}
