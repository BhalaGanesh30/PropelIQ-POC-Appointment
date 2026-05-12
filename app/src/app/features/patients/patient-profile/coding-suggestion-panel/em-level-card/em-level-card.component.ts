import {
  ChangeDetectionStrategy,
  Component,
  input,
} from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DecimalPipe } from '@angular/common';

import type { EmSuggestionDto } from '../../../../../shared/models/cpt-suggestion.model';

/**
 * E/M (Evaluation & Management) level suggestion card (US_050 / AC-3 / UXR-108).
 *
 * Displays:
 * - E/M level code badge in monospace with purple tint (distinct from CPT blue tint)
 * - Description and confidence bar
 * - Collapsible mat-expansion-panel listing contributing clinical complexity factors
 * - MatTooltip (UXR-204) on each factor for factor definitions
 */
@Component({
  selector: 'app-em-level-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatProgressBarModule,
    MatIconModule,
    MatExpansionModule,
    MatTooltipModule,
    DecimalPipe,
  ],
  template: `
    <mat-card class="em-card" appearance="outlined">
      <mat-card-header>
        <span class="ai-badge" aria-label="AI-generated E/M level">
          <mat-icon aria-hidden="true" class="ai-badge__icon">auto_awesome</mat-icon>
          AI-generated · E/M Level
        </span>
      </mat-card-header>

      <mat-card-content>
        <div class="em-card__header">
          <span class="code-badge code-badge--em">{{ emSuggestion().emLevel }}</span>
          <span class="em-card__description">{{ emSuggestion().description }}</span>
        </div>

        <div class="em-card__confidence">
          <label class="confidence-label" [id]="'em-confidence-' + emSuggestion().decisionId">
            Confidence
          </label>
          <mat-progress-bar
            mode="determinate"
            [value]="emSuggestion().confidence * 100"
            [attr.aria-labelledby]="'em-confidence-' + emSuggestion().decisionId"
          />
          <span class="confidence-value">{{ emSuggestion().confidence * 100 | number:'1.0-0' }}%</span>
        </div>

        <p class="em-card__rationale">{{ emSuggestion().rationale }}</p>

        @if (emSuggestion().complexityFactors.length > 0) {
          <mat-expansion-panel class="em-card__factors-panel" hideToggle>
            <mat-expansion-panel-header>
              <mat-panel-title class="em-card__factors-title">
                <mat-icon aria-hidden="true" class="em-card__factors-icon">list_alt</mat-icon>
                Contributing Factors ({{ emSuggestion().complexityFactors.length }})
              </mat-panel-title>
            </mat-expansion-panel-header>

            <ul class="em-card__factors-list" role="list">
              @for (factor of emSuggestion().complexityFactors; track factor) {
                <li
                  class="em-card__factor-item"
                  role="listitem"
                  [matTooltip]="factor"
                  matTooltipPosition="above"
                  matTooltipShowDelay="300"
                >
                  <mat-icon aria-hidden="true" class="em-card__factor-icon">fiber_manual_record</mat-icon>
                  <span class="em-card__factor-text">{{ factor }}</span>
                </li>
              }
            </ul>
          </mat-expansion-panel>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    :host { display: block; }

    .em-card {
      --mdc-outlined-card-container-color: #f3e5f5;
      border-color: #ce93d8;
    }

    mat-card-header {
      padding-bottom: 4px;
    }

    .ai-badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 11px;
      font-weight: 500;
      background: #f3e5f5;
      color: #6a1b9a;
    }

    .ai-badge__icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }

    .em-card__header {
      display: flex;
      align-items: baseline;
      gap: 12px;
      margin-bottom: 12px;
    }

    .code-badge {
      font-family: 'JetBrains Mono', 'Fira Code', 'Courier New', monospace;
      font-size: 15px;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 4px;
      white-space: nowrap;
      letter-spacing: 0.04em;
    }

    /* Purple tint distinguishes E/M level from CPT blue (AC-3 / UXR-108) */
    .code-badge--em {
      background: #f3e5f5;
      color: #6a1b9a;
    }

    .em-card__description {
      font-size: 14px;
      color: var(--color-neutral-800, #424242);
      flex: 1;
    }

    .em-card__confidence {
      display: grid;
      grid-template-columns: auto 1fr auto;
      align-items: center;
      gap: 8px;
      margin-bottom: 10px;
    }

    .confidence-label {
      font-size: 12px;
      color: var(--color-neutral-600, #757575);
      white-space: nowrap;
    }

    .confidence-value {
      font-size: 12px;
      font-weight: 600;
      color: var(--color-neutral-700, #616161);
      min-width: 36px;
      text-align: right;
    }

    .em-card__rationale {
      font-size: 13px;
      color: var(--color-neutral-700, #616161);
      line-height: 1.5;
      margin: 0 0 12px 0;
    }

    .em-card__factors-panel {
      background: transparent;
      box-shadow: none;
      border: 1px solid #ce93d8;
      border-radius: 8px;
    }

    .em-card__factors-title {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 13px;
      font-weight: 500;
      color: #6a1b9a;
    }

    .em-card__factors-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }

    .em-card__factors-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .em-card__factor-item {
      display: flex;
      align-items: flex-start;
      gap: 6px;
      cursor: default;
    }

    .em-card__factor-icon {
      font-size: 8px;
      width: 8px;
      height: 8px;
      margin-top: 5px;
      color: #6a1b9a;
      flex-shrink: 0;
    }

    .em-card__factor-text {
      font-size: 13px;
      color: var(--color-neutral-700, #616161);
      line-height: 1.4;
    }
  `],
})
export class EmLevelCardComponent {
  readonly emSuggestion = input.required<EmSuggestionDto>();
}
