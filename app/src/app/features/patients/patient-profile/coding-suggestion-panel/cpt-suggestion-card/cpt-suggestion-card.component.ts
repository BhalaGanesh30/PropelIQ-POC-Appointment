import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { DecimalPipe } from '@angular/common';

import { CodingDecisionFacade } from '../../../../../features/patients/coding-decision.facade';
import { InlineCodeEditComponent } from '../inline-code-edit/inline-code-edit.component';
import { RejectConfirmationDialogComponent } from '../reject-confirmation-dialog/reject-confirmation-dialog.component';
import type { CptSuggestionDto } from '../../../../../shared/models/cpt-suggestion.model';

/**
 * Individual CPT procedure code suggestion card with Accept / Modify / Reject workflow
 * (US_050 / US_051 / SCR-017).
 *
 * Identical decision-workflow interaction pattern as `SuggestionCardComponent` (UXR-108).
 * Visual states:
 * - pending:  CPT code badge, confidence bar, rationale, action buttons.
 * - accepted: Green border, "Accepted" chip, "Edit Decision" button (Edge Case 1).
 * - modified: "Modified from AI" chip, "Edit Decision" button.
 * - rejected: Gray card, strikethrough, "Search Code" link → SCR-018 (AC-3).
 * - editing:  InlineCodeEditComponent with amber outline (AC-2).
 */
@Component({
  selector: 'app-cpt-suggestion-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatChipsModule,
    MatProgressBarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    DecimalPipe,
    InlineCodeEditComponent,
  ],
  template: `
    <mat-card
      class="cpt-card"
      appearance="outlined"
      [class.cpt-card--accepted]="resolvedState() === 'accepted' || resolvedState() === 'modified'"
      [class.cpt-card--rejected]="resolvedState() === 'rejected'"
    >
      <mat-card-header>
        <span class="ai-badge" aria-label="AI-generated content">
          <mat-icon aria-hidden="true" class="ai-badge__icon">auto_awesome</mat-icon>
          AI-generated
        </span>

        @if (resolvedState() === 'accepted') {
          <mat-chip class="state-chip state-chip--accepted" disabled>Accepted</mat-chip>
        } @else if (resolvedState() === 'modified') {
          <mat-chip class="state-chip state-chip--modified" disabled>Modified from AI</mat-chip>
        } @else if (resolvedState() === 'rejected') {
          <mat-chip class="state-chip state-chip--rejected" disabled>Rejected</mat-chip>
        }
      </mat-card-header>

      <mat-card-content>
        <div class="cpt-card__header">
          <span
            [id]="'cpt-code-' + suggestion().decisionId"
            class="code-badge"
            [class.code-badge--rejected]="resolvedState() === 'rejected'"
          >{{ suggestion().cptCode }}</span>
          <span
            class="cpt-card__description"
            [class.cpt-card__description--rejected]="resolvedState() === 'rejected'"
          >{{ suggestion().description }}</span>
        </div>

        @if (resolvedState() !== 'rejected') {
          <div class="cpt-card__confidence">
            <label class="confidence-label" [id]="'cpt-confidence-' + suggestion().decisionId">
              Confidence
            </label>
            <mat-progress-bar
              mode="determinate"
              [value]="suggestion().confidence * 100"
              [attr.aria-labelledby]="'cpt-confidence-' + suggestion().decisionId"
            />
            <span class="confidence-value">{{ suggestion().confidence * 100 | number:'1.0-0' }}%</span>
          </div>

          <p class="cpt-card__rationale">{{ suggestion().rationale }}</p>
        }

        @if (resolvedState() === 'editing') {
          <app-inline-code-edit
            [currentCode]="editingCode()"
            [currentDescription]="editingDescription()"
            (saved)="onModifySaved($event)"
            (cancelled)="editingActive.set(false)"
          />
        }
      </mat-card-content>

      <mat-card-actions align="end">
        @switch (resolvedState()) {
          @case ('pending') {
            <button
              mat-stroked-button
              type="button"
              [disabled]="suggestion().citations.length === 0"
              [matTooltip]="suggestion().citations.length === 0 ? 'No supporting evidence available' : ''"
              (click)="viewEvidence.emit(suggestion())"
            >
              <mat-icon aria-hidden="true">article</mat-icon>
              View Evidence
            </button>

            <button
              mat-flat-button
              type="button"
              class="btn-accept"
              [attr.aria-describedby]="'cpt-code-' + suggestion().decisionId"
              (click)="onAccept()"
            >
              <mat-icon aria-hidden="true">check_circle</mat-icon>
              Accept
            </button>

            <button
              mat-stroked-button
              type="button"
              [attr.aria-describedby]="'cpt-code-' + suggestion().decisionId"
              (click)="onModifyClick()"
            >
              <mat-icon aria-hidden="true">edit</mat-icon>
              Modify
            </button>

            <button
              #rejectBtn
              mat-stroked-button
              type="button"
              color="warn"
              [attr.aria-describedby]="'cpt-code-' + suggestion().decisionId"
              (click)="onReject()"
            >
              <mat-icon aria-hidden="true">cancel</mat-icon>
              Reject
            </button>
          }

          @case ('editing') {
            <!-- Handled by InlineCodeEditComponent -->
          }

          @case ('accepted') {
            <button mat-stroked-button type="button" (click)="onEditDecision()">
              <mat-icon aria-hidden="true">edit</mat-icon>
              Edit Decision
            </button>
          }

          @case ('modified') {
            <button mat-stroked-button type="button" (click)="onEditDecision()">
              <mat-icon aria-hidden="true">edit</mat-icon>
              Edit Decision
            </button>
          }

          @case ('rejected') {
            <a mat-stroked-button routerLink="/coding/search">
              <mat-icon aria-hidden="true">search</mat-icon>
              Search Code
            </a>
          }
        }
      </mat-card-actions>
    </mat-card>
  `,
  styles: [`
    :host { display: block; }

    .cpt-card {
      --mdc-outlined-card-container-color: var(--color-ai-tint, #e8eaf6);
      transition: border-color 0.2s ease, background 0.2s ease;
    }

    .cpt-card--accepted {
      border-color: #2e7d32 !important;
    }

    .cpt-card--rejected {
      --mdc-outlined-card-container-color: #f5f5f5;
      border-color: #bdbdbd !important;
      opacity: 0.75;
    }

    mat-card-header {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 8px;
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
      background: var(--color-ai-tint, #e8eaf6);
      color: var(--color-primary-700, #303f9f);
    }

    .ai-badge__icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }

    .state-chip { margin-left: 8px; font-size: 11px; }
    .state-chip--accepted { --mdc-chip-label-text-color: #1b5e20; background: #e8f5e9 !important; }
    .state-chip--modified { --mdc-chip-label-text-color: #e65100; background: #fff3e0 !important; }
    .state-chip--rejected { --mdc-chip-label-text-color: #616161; background: #eeeeee !important; }

    .cpt-card__header {
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
      background: var(--color-primary-50, #e8eaf6);
      color: var(--color-primary-800, #1a237e);
      white-space: nowrap;
      letter-spacing: 0.04em;
    }

    .code-badge--rejected {
      text-decoration: line-through;
      color: var(--color-neutral-500, #9e9e9e);
    }

    .cpt-card__description {
      font-size: 14px;
      color: var(--color-neutral-800, #424242);
      flex: 1;
    }

    .cpt-card__description--rejected {
      text-decoration: line-through;
      color: var(--color-neutral-500, #9e9e9e);
    }

    .cpt-card__confidence {
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

    .cpt-card__rationale {
      font-size: 13px;
      color: var(--color-neutral-700, #616161);
      line-height: 1.5;
      margin: 0;
    }

    .btn-accept {
      background-color: #2e7d32;
      color: #fff;
    }
  `],
})
export class CptSuggestionCardComponent {
  readonly suggestion = input.required<CptSuggestionDto>();
  readonly viewEvidence = output<CptSuggestionDto>();

  protected readonly decisionFacade = inject(CodingDecisionFacade);
  private readonly dialog = inject(MatDialog);

  readonly editingActive = signal(false);
  readonly editingCode = signal('');
  readonly editingDescription = signal('');

  readonly resolvedState = () => {
    if (this.editingActive()) {
      return 'editing';
    }
    return this.decisionFacade.decisions()[this.suggestion().decisionId]?.state ?? 'pending';
  };

  protected onAccept(): void {
    this.decisionFacade.accept(
      this.suggestion().decisionId,
      this.suggestion().cptCode,
      this.suggestion().description,
    );
  }

  protected onModifyClick(): void {
    const entry = this.decisionFacade.decisions()[this.suggestion().decisionId];
    this.editingCode.set(entry?.finalCode ?? this.suggestion().cptCode);
    this.editingDescription.set(entry?.finalDescription ?? this.suggestion().description);
    this.editingActive.set(true);
  }

  protected onModifySaved(event: { code: string; description: string }): void {
    this.editingActive.set(false);
    this.decisionFacade.modify(this.suggestion().decisionId, {
      decisionId: this.suggestion().decisionId,
      finalCode: event.code,
      finalDescription: event.description,
    });
  }

  protected onReject(): void {
    const ref = this.dialog.open(RejectConfirmationDialogComponent, {
      disableClose: true,
      autoFocus: 'first-tabbable',
      restoreFocus: true,
    });
    ref.afterClosed().subscribe((confirmed: boolean) => {
      if (confirmed) {
        this.decisionFacade.reject(this.suggestion().decisionId);
      }
    });
  }

  /** Edge Case 1: re-open inline edit pre-populated with accepted / modified code. */
  protected onEditDecision(): void {
    const entry = this.decisionFacade.decisions()[this.suggestion().decisionId];
    this.editingCode.set(entry?.finalCode ?? this.suggestion().cptCode);
    this.editingDescription.set(entry?.finalDescription ?? this.suggestion().description);
    this.editingActive.set(true);
  }
}
