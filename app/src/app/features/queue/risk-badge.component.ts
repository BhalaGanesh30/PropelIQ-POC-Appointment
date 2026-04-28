import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RiskFeature, RiskLevel, RISK_COLORS, RISK_LABELS } from './models/risk-score.models';

/**
 * Standalone colour-coded risk badge chip (AC-2, US_028 task_003).
 *
 * Displays a mat-chip whose background colour reflects the risk level:
 *   Green  (#388E3C) — Low risk   (UXR-404 success)
 *   Amber  (#E65100) — Medium risk (UXR-404 warning)
 *   Red    (#C62828) — High risk  (UXR-404 error)
 *   Grey   (#616161) — Unknown    (UXR-404 neutral)
 *
 * All colours pass WCAG AA 4.5:1 against white text (UXR-201).
 * Hovering the badge shows a tooltip listing the explainable feature
 * contributions so staff understand the risk reasoning.
 * UXR-203: aria-label is applied for screen-reader consumers.
 */
@Component({
  selector: 'app-risk-badge',
  standalone: true,
  imports: [MatChipsModule, MatTooltipModule],
  templateUrl: './risk-badge.component.html',
  styleUrl: './risk-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RiskBadgeComponent {
  /** Risk classification to display. */
  riskLevel = input.required<RiskLevel>();
  /** Explainable feature contributions (AIR-004). Shown in the tooltip. */
  features = input<RiskFeature[]>([]);

  /** AC-2: Background colour from the UXR-404 semantic palette. */
  readonly badgeColor = computed(() => RISK_COLORS[this.riskLevel()]);
  /** Human-readable label rendered inside the chip. */
  readonly badgeLabel = computed(() => RISK_LABELS[this.riskLevel()]);
  /** UXR-203: Accessible label for screen readers. */
  readonly ariaLabel = computed(() => `No-show risk: ${this.badgeLabel()}`);

  /**
   * Tooltip text — feature contributions list for known risk levels,
   * "Scoring unavailable" for Unknown (edge case 1).
   */
  readonly tooltipText = computed(() => {
    if (this.riskLevel() === 'Unknown') {
      return 'Scoring unavailable';
    }
    const feats = this.features();
    if (feats.length === 0) return this.badgeLabel();
    return feats.map((f) => `${f.name}: ${f.contribution}`).join('\n');
  });
}
