import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  signal,
} from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DatePipe, KeyValuePipe } from '@angular/common';
import { ConfigApiService } from './config-api.service';
import { ConflictDialogComponent } from './conflict-dialog.component';
import { ConfigCategory, ConfigSnapshot } from './models/config.models';

/**
 * Renders a reactive form for a single configuration category (US_059, AC-1, AC-2).
 *
 * States:
 * - Loading: skeleton placeholder shown while GET is in flight.
 * - Default: form with current values; Save and Reset buttons.
 * - Saving: spinner on button, controls disabled.
 * - Error: inline error via snack-bar; form remains editable.
 * - Conflict (edge case 1): ConflictDialogComponent opens to let admin decide.
 *
 * AC-2: 422 response errors are displayed in a snack-bar with descriptive messages.
 * Edge case 1: 409 response opens the conflict resolution dialog.
 */
@Component({
  selector: 'app-config-category',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    KeyValuePipe,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './config-category.component.html',
})
export class ConfigCategoryComponent implements OnInit {
  readonly category = input.required<ConfigCategory>();

  private readonly api      = inject(ConfigApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog   = inject(MatDialog);

  readonly form     = signal<FormGroup | null>(null);
  readonly loading  = signal(true);
  readonly saving   = signal(false);
  readonly error    = signal<string | null>(null);
  readonly snapshot = signal<ConfigSnapshot | null>(null);
  readonly etag     = signal('0');

  ngOnInit(): void {
    this.loadConfig();
  }

  loadConfig(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getCurrent(this.category()).subscribe({
      next: (response) => {
        const config = response.body!;
        this.snapshot.set(config);
        // Strip quotes from ETag header value; fall back to version number.
        const raw = response.headers.get('ETag') ?? `"${config.versionNumber}"`;
        this.etag.set(raw.replace(/"/g, ''));
        this.buildForm(config.values);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load configuration. Click Reset to retry.');
        this.loading.set(false);
      },
    });
  }

  save(): void {
    const f = this.form();
    if (!f || f.invalid || this.saving()) return;

    this.saving.set(true);
    this.api.update(this.category(), f.getRawValue(), this.etag()).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.etag.set(String(result.versionNumber));
        // Update cached snapshot version number so history button reflects new state.
        const snap = this.snapshot();
        if (snap) {
          this.snapshot.set({ ...snap, versionNumber: result.versionNumber });
        }
        this.snackBar.open(
          `Configuration saved — version ${result.versionNumber}`,
          'Dismiss',
          { duration: 4000 },
        );
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        if (err.status === 409) {
          this.handleConflict(err.error as ConfigSnapshot);
        } else if (err.status === 422) {
          const errors: string[] = Array.isArray(err.error) ? err.error : [String(err.error)];
          this.snackBar.open(errors.join(' • '), 'Dismiss', { duration: 8000 });
        } else {
          this.snackBar.open('Save failed — please try again.', 'Retry', {
            duration: 5000,
          });
        }
      },
    });
  }

  private handleConflict(currentValue: ConfigSnapshot): void {
    const dialogRef = this.dialog.open(ConflictDialogComponent, {
      data: {
        yourValues:    this.form()?.getRawValue() ?? {},
        currentValues: currentValue.values,
        updatedBy:     currentValue.updatedByName,
      },
      width: '520px',
    });

    dialogRef.afterClosed().subscribe((confirmed: boolean) => {
      if (confirmed) {
        // Admin chose to overwrite: update ETag to server's current version and retry.
        this.etag.set(String(currentValue.versionNumber));
        this.save();
      } else {
        // Admin cancelled: reset form to server's current values.
        this.etag.set(String(currentValue.versionNumber));
        this.snapshot.set(currentValue);
        this.buildForm(currentValue.values);
      }
    });
  }

  private buildForm(values: Record<string, unknown>): void {
    const controls: Record<string, FormControl> = {};
    for (const [key, val] of Object.entries(values)) {
      controls[key] = new FormControl(val !== null ? String(val) : '');
    }
    this.form.set(new FormGroup(controls));
  }
}
