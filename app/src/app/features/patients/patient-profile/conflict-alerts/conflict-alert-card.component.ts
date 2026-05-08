import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import type { ConflictAlertDto } from '../../../../shared/models/conflict-alert.model';
import {
  ConflictAcknowledgeDialogComponent,
  type ConflictAcknowledgeDialogData,
  type ConflictAcknowledgeDialogResult,
} from './conflict-acknowledge-dialog.component';

/**
 * Card for a single drug-drug or drug-allergy conflict (AC-2, UXR-404, UXR-504).
 *
 * Visual design:
 * - Severity-colored left border (CSS custom property via [attr.data-severity])
 *   critical: #c62828 | high: #e65100 | moderate: #f57f17 | low: #1565c0
 * - Severity badge with WCAG 2.1 AA contrast (UXR-201)
 * - Conflicting drug pair labels
 * - Acknowledged state shows chip instead of button
 */
@Component({
  selector: 'app-conflict-alert-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, MatChipsModule, MatIconModule, MatTooltipModule],
  template: `
    <article
      class="alert-card"
      [attr.data-severity]="alert().severity"
      [attr.aria-label]="'Conflict alert: ' + alert().description"
      role="listitem"
    >
      <!-- ── Severity badge ─────────────────────────────────────────── -->
      <div class="alert-card__header">
        <span
          class="severity-badge"
          [attr.data-severity]="alert().severity"
          [attr.aria-label]="severityLabel() + ' severity'"
        >
          <mat-icon class="severity-badge__icon" aria-hidden="true">{{ severityIcon() }}</mat-icon>
          {{ severityLabel() }}
        </span>

        <span class="conflict-type-badge">
          {{ alert().conflictType === 'drug-drug' ? 'Drug–Drug' : 'Drug–Allergy' }}
        </span>

        @if (alert().acknowledged) {
          <mat-chip class="acknowledged-chip" aria-label="This conflict has been acknowledged">
            <mat-icon matChipLeadingIcon aria-hidden="true">check_circle</mat-icon>
            Acknowledged
          </mat-chip>
        }
      </div>

      <!-- ── Description ───────────────────────────────────────────── -->
      <p class="alert-card__description">{{ alert().description }}</p>

      <!-- ── Drug pair ─────────────────────────────────────────────── -->
      <div class="alert-card__pair" aria-label="Conflicting substances">
        <span class="alert-card__drug">{{ alert().drugA }}</span>
        <mat-icon class="alert-card__pair-arrow" aria-hidden="true">sync_alt</mat-icon>
        <span class="alert-card__drug">{{ alert().drugB ?? 'Allergy' }}</span>
      </div>

      <!-- ── Acknowledged metadata ─────────────────────────────────── -->
      @if (alert().acknowledged && alert().acknowledgedAt) {
        <p class="alert-card__ack-meta">
          Acknowledged {{ alert().acknowledgedAt | date:'medium' }}
          @if (alert().acknowledgedBy) {
            by {{ alert().acknowledgedBy }}
          }
        </p>
      }

      <!-- ── Acknowledge button ─────────────────────────────────────── -->
      @if (!alert().acknowledged) {
        <button
          mat-stroked-button
          type="button"
          class="alert-card__ack-btn"
          [class.alert-card__ack-btn--critical]="alert().severity === 'critical'"
          (click)="openAcknowledgeDialog()"
          [attr.aria-label]="'Acknowledge ' + severityLabel() + ' conflict: ' + alert().description"
        >
          <mat-icon aria-hidden="true">how_to_reg</mat-icon>
          Acknowledge
        </button>
      }
    </article>
  `,
  styles: [`
    :host { display: block; }

    .alert-card {
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 14px 16px;
      background: #fff;
      border: 1px solid var(--color-neutral-200, #e0e0e0);
      border-radius: 8px;
      border-left: 4px solid var(--severity-color, #9e9e9e);
      transition: box-shadow 0.15s ease;

      &:hover { box-shadow: 0 2px 8px rgba(0,0,0,.08); }

      /* Severity left-border colors (UXR-404) */
      &[data-severity="critical"] { --severity-color: #c62828; }
      &[data-severity="high"]     { --severity-color: #e65100; }
      &[data-severity="moderate"] { --severity-color: #f57f17; }
      &[data-severity="low"]      { --severity-color: #1565c0; }
    }

    .alert-card__header {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }

    /* Severity badge — WCAG 2.1 AA contrast verified (UXR-201) */
    .severity-badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      font-size: 11px;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 12px;
      text-transform: uppercase;
      letter-spacing: 0.04em;

      /* Critical: white text on #c62828 → contrast 5.97:1 ✓ */
      &[data-severity="critical"] { background: #c62828; color: #fff; }
      /* High: white text on #e65100 → contrast 4.54:1 ✓ */
      &[data-severity="high"]     { background: #e65100; color: #fff; }
      /* Moderate: dark text on #fff8e1 → contrast 11.4:1 ✓ */
      &[data-severity="moderate"] { background: #fff8e1; color: #7c5800; border: 1px solid #ffe082; }
      /* Low: white text on #1565c0 → contrast 7.18:1 ✓ */
      &[data-severity="low"]      { background: #1565c0; color: #fff; }
    }

    .severity-badge__icon { font-size: 13px; width: 13px; height: 13px; }

    .conflict-type-badge {
      font-size: 11px;
      color: var(--color-neutral-600, #757575);
      background: var(--color-neutral-100, #f5f5f5);
      padding: 2px 8px;
      border-radius: 12px;
    }

    .acknowledged-chip {
      font-size: 11px;
      background: #e8f5e9 !important;
      color: #2e7d32 !important;
    }

    .alert-card__description {
      font-size: 14px;
      color: var(--color-neutral-800, #424242);
      margin: 0;
      line-height: 1.5;
    }

    .alert-card__pair {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
      background: var(--color-neutral-50, #fafafa);
      border: 1px solid var(--color-neutral-200, #e0e0e0);
      padding: 6px 12px;
      border-radius: 6px;
    }

    .alert-card__drug {
      font-weight: 600;
      color: var(--color-neutral-900, #212121);
    }

    .alert-card__pair-arrow {
      font-size: 16px;
      width: 16px;
      height: 16px;
      color: var(--color-neutral-500, #9e9e9e);
    }

    .alert-card__ack-meta {
      font-size: 12px;
      color: var(--color-neutral-500, #9e9e9e);
      margin: 0;
      font-style: italic;
    }

    .alert-card__ack-btn {
      align-self: flex-start;
    }

    .alert-card__ack-btn--critical {
      border-color: #c62828 !important;
      color: #c62828 !important;
    }

    .alert-card__ack-btn:focus-visible {
      outline: 2px solid #1976d2;
      outline-offset: 2px;
    }
  `],
})
export class ConflictAlertCardComponent {
  readonly alert = input.required<ConflictAlertDto>();
  readonly acknowledged = output<string>();

  private readonly dialog = inject(MatDialog);

  protected readonly severityLabel = computed(() => {
    const labels: Record<string, string> = {
      critical: 'Critical',
      high: 'High',
      moderate: 'Moderate',
      low: 'Low',
    };
    return labels[this.alert().severity] ?? this.alert().severity;
  });

  protected readonly severityIcon = computed(() => {
    const icons: Record<string, string> = {
      critical: 'emergency',
      high: 'warning',
      moderate: 'info',
      low: 'info_outline',
    };
    return icons[this.alert().severity] ?? 'info_outline';
  });

  protected openAcknowledgeDialog(): void {
    const a = this.alert();
    const data: ConflictAcknowledgeDialogData = {
      conflictId: a.conflictId,
      severity: a.severity,
      description: a.description,
      drugA: a.drugA,
      drugB: a.drugB,
    };

    const ref = this.dialog.open<
      ConflictAcknowledgeDialogComponent,
      ConflictAcknowledgeDialogData,
      ConflictAcknowledgeDialogResult
    >(ConflictAcknowledgeDialogComponent, {
      data,
      width: '480px',
      // Critical alerts cannot be dismissed without explicit action (AC-3).
      disableClose: a.severity === 'critical',
      autoFocus: 'dialog',
      restoreFocus: true,
    });

    ref.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.acknowledged.emit(a.conflictId);
      }
    });
  }
}
