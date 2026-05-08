import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { A11yModule } from '@angular/cdk/a11y';

import type { ConflictSeverity } from '../../../../shared/models/conflict-alert.model';

/** Data injected via MAT_DIALOG_DATA. */
export interface ConflictAcknowledgeDialogData {
  conflictId: string;
  severity: ConflictSeverity;
  description: string;
  drugA: string;
  drugB: string | null;
}

/** Value emitted when the dialog closes (true = confirmed, false = cancelled). */
export type ConflictAcknowledgeDialogResult = boolean;

/** Typed confirmation text required for Critical-severity acknowledgment (AC-3, UXR-111). */
const REQUIRED_TEXT = 'ACKNOWLEDGE';

/**
 * Mandatory acknowledgment dialog for clinical conflict alerts (AC-3, UXR-111).
 *
 * For Critical severity: the clinician must type "ACKNOWLEDGE" before confirming.
 * cdkTrapFocus keeps keyboard focus trapped within the dialog (UXR-206).
 * MatDialog automatically returns focus to the trigger element on close (UXR-206).
 */
@Component({
  selector: 'app-conflict-acknowledge-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    A11yModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
  ],
  template: `
    <div cdkTrapFocus cdkTrapFocusAutoCapture role="dialog" [attr.aria-labelledby]="dialogTitleId">

      <!-- Title -->
      <h2 [id]="dialogTitleId" mat-dialog-title class="dialog-title" [attr.data-severity]="data.severity">
        <mat-icon class="dialog-title__icon" aria-hidden="true">{{ severityIcon() }}</mat-icon>
        <span>{{ severityLabel() }} Conflict Alert</span>
      </h2>

      <!-- Content -->
      <mat-dialog-content>
        <p class="dialog-description">{{ data.description }}</p>
        <div class="dialog-drug-pair">
          <span class="dialog-drug-pair__label">Conflicting:</span>
          <strong>{{ data.drugA }}</strong>
          @if (data.drugB) {
            <span class="dialog-drug-pair__sep">↔</span>
            <strong>{{ data.drugB }}</strong>
          }
        </div>

        <!-- Typed confirmation for Critical severity (SCR-016 Validation state) -->
        @if (isCritical()) {
          <div class="dialog-confirm-input">
            <p class="dialog-confirm-input__hint">
              Type <strong>{{ requiredText }}</strong> to confirm you have reviewed this critical alert.
            </p>
            <mat-form-field appearance="outline" class="dialog-confirm-input__field">
              <mat-label>Type ACKNOWLEDGE to confirm</mat-label>
              <input
                matInput
                [ngModel]="confirmText()"
                (ngModelChange)="confirmText.set($event)"
                [attr.aria-label]="'Type ' + requiredText + ' to enable acknowledgment button'"
                autocomplete="off"
                spellcheck="false"
              />
            </mat-form-field>
          </div>
        }
      </mat-dialog-content>

      <!-- Actions -->
      <mat-dialog-actions align="end">
        <button
          mat-stroked-button
          type="button"
          (click)="cancel()"
          aria-label="Cancel and return to patient profile"
        >
          {{ isCritical() ? 'Review Later' : 'Cancel' }}
        </button>
        <button
          mat-raised-button
          color="warn"
          type="button"
          [disabled]="!canConfirm()"
          (click)="confirm()"
          [attr.aria-label]="'Acknowledge ' + data.severity + ' conflict: ' + data.description"
        >
          <mat-icon aria-hidden="true">check_circle</mat-icon>
          Acknowledge
        </button>
      </mat-dialog-actions>

    </div>
  `,
  styles: [`
    .dialog-title {
      display: flex;
      align-items: center;
      gap: 10px;
      margin-bottom: 0;

      &[data-severity="critical"] { color: #c62828; }
      &[data-severity="high"]     { color: #e65100; }
      &[data-severity="moderate"] { color: #f57f17; }
      &[data-severity="low"]      { color: #1565c0; }
    }

    .dialog-title__icon { font-size: 22px; width: 22px; height: 22px; }

    .dialog-description {
      font-size: 14px;
      color: var(--color-neutral-800, #424242);
      margin: 0 0 12px;
    }

    .dialog-drug-pair {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 14px;
      background: var(--color-neutral-100, #f5f5f5);
      padding: 8px 12px;
      border-radius: 6px;
      flex-wrap: wrap;
    }

    .dialog-drug-pair__label {
      color: var(--color-neutral-600, #757575);
      font-size: 12px;
      font-weight: 500;
    }

    .dialog-drug-pair__sep { color: var(--color-neutral-500, #9e9e9e); }

    .dialog-confirm-input {
      margin-top: 16px;
    }

    .dialog-confirm-input__hint {
      font-size: 13px;
      color: var(--color-neutral-700, #616161);
      margin: 0 0 8px;
    }

    .dialog-confirm-input__field { width: 100%; }

    button[mat-raised-button][disabled] {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `],
})
export class ConflictAcknowledgeDialogComponent {
  protected readonly data = inject<ConflictAcknowledgeDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject(MatDialogRef<ConflictAcknowledgeDialogComponent>);

  protected readonly requiredText = REQUIRED_TEXT;
  protected readonly dialogTitleId = `conflict-dialog-title-${this.data.conflictId}`;

  protected readonly confirmText = signal('');

  protected readonly isCritical = computed(() => this.data.severity === 'critical');

  /** Confirm is enabled when: non-critical (no typed text needed) OR typed text matches. */
  protected readonly canConfirm = computed(() =>
    !this.isCritical() || this.confirmText().trim().toUpperCase() === REQUIRED_TEXT,
  );

  protected readonly severityLabel = computed(() => {
    const labels: Record<string, string> = {
      critical: 'Critical',
      high: 'High',
      moderate: 'Moderate',
      low: 'Low',
    };
    return labels[this.data.severity] ?? this.data.severity;
  });

  protected readonly severityIcon = computed(() => {
    const icons: Record<string, string> = {
      critical: 'emergency',
      high: 'warning',
      moderate: 'info',
      low: 'info_outline',
    };
    return icons[this.data.severity] ?? 'info_outline';
  });

  protected confirm(): void {
    if (!this.canConfirm()) return;
    this.dialogRef.close(true);
  }

  protected cancel(): void {
    this.dialogRef.close(false);
  }
}
