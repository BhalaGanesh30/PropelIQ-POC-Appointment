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
import type { IcdSuggestionDto } from '../../../../../shared/models/coding-suggestion.model';

/**
 * Individual ICD-10 suggestion card with Accept / Modify / Reject workflow (SCR-017 / US_051).
 *
 * Visual states (UXR-108):
 * - pending:  AI badge + confidence bar + action buttons at card bottom.
 * - accepted: Green `mat-card` border; "Accepted" `mat-chip`; "Edit Decision" button (Edge Case 1).
 * - modified: Same green border; "Modified from AI" `mat-chip`; "Edit Decision" button.
 * - rejected: Gray `mat-card`; strikethrough code + description; "Search Code" link → SCR-018 (AC-3).
 * - editing:  InlineCodeEditComponent renders inside card (AC-2); amber border active.
 *
 * Accessibility:
 * - Action buttons use `aria-describedby` pointing to the card code label (UXR-205).
 * - `restoreFocus: true` on the reject dialog call returns focus to the trigger (UXR-206).
 */
@Component({
  selector: 'app-suggestion-card',
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
      class="suggestion-card"
      appearance="outlined"
      [class.suggestion-card--accepted]="resolvedState() === 'accepted' || resolvedState() === 'modified'"
      [class.suggestion-card--rejected]="resolvedState() === 'rejected'"
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
        <div class="suggestion-card__header">
          <span
            [id]="'icd-code-' + suggestion().decisionId"
            class="code-badge"
            [class.code-badge--rejected]="resolvedState() === 'rejected'"
          >{{ suggestion().icdCode }}</span>
          <span
            class="suggestion-card__description"
            [class.suggestion-card__description--rejected]="resolvedState() === 'rejected'"
          >{{ suggestion().description }}</span>
        </div>

        @if (resolvedState() !== 'rejected') {
          <div class="suggestion-card__confidence">
            <label class="confidence-label" [id]="'confidence-' + suggestion().decisionId">
              Confidence
            </label>
            <mat-progress-bar
              mode="determinate"
              [value]="suggestion().confidence * 100"
              [attr.aria-labelledby]="'confidence-' + suggestion().decisionId"
            />
            <span class="confidence-value">{{ suggestion().confidence * 100 | number:'1.0-0' }}%</span>
          </div>

          <p class="suggestion-card__rationale">{{ suggestion().rationale }}</p>
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
            <!-- Evidence button -->
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

            <!-- Accept -->
            <button
              mat-flat-button
              type="button"
              class="btn-accept"
              [attr.aria-describedby]="'icd-code-' + suggestion().decisionId"
              (click)="onAccept()"
            >
              <mat-icon aria-hidden="true">check_circle</mat-icon>
              Accept
            </button>

            <!-- Modify -->
            <button
              mat-stroked-button
              type="button"
              [attr.aria-describedby]="'icd-code-' + suggestion().decisionId"
              (click)="onModifyClick()"
            >
              <mat-icon aria-hidden="true">edit</mat-icon>
              Modify
            </button>

            <!-- Reject -->
            <button
              #rejectBtn
              mat-stroked-button
              type="button"
              color="warn"
              [attr.aria-describedby]="'icd-code-' + suggestion().decisionId"
              (click)="onReject()"
            >
              <mat-icon aria-hidden="true">cancel</mat-icon>
              Reject
            </button>
          }

          @case ('editing') {
            <!-- Actions handled by InlineCodeEditComponent above -->
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

    .suggestion-card {
      --mdc-outlined-card-container-color: var(--color-surface, #fff);
      transition: border-color 0.2s ease, background 0.2s ease;
    }

    /* Accepted / modified — green border (SCR-017 Validation state). */
    .suggestion-card--accepted {
      border-color: #2e7d32 !important;
    }

    /* Rejected — gray card + strikethrough (SCR-017 Validation state). */
    .suggestion-card--rejected {
      --mdc-outlined-card-container-color: #f5f5f5;
      border-color: #bdbdbd !important;
      opacity: 0.75;
    }

    .ai-badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.5px;
      text-transform: uppercase;
      background: var(--color-ai-tint, #e8eaf6);
      color: var(--color-ai-text, #3949ab);
    }

    .ai-badge__icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }

    /* State chips */
    .state-chip { margin-left: 8px; font-size: 11px; }
    .state-chip--accepted { --mdc-chip-label-text-color: #1b5e20; background: #e8f5e9 !important; }
    .state-chip--modified { --mdc-chip-label-text-color: #e65100; background: #fff3e0 !important; }
    .state-chip--rejected { --mdc-chip-label-text-color: #616161; background: #eeeeee !important; }

    mat-card-header {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 8px;
    }

    .suggestion-card__header {
      display: flex;
      align-items: baseline;
      gap: 12px;
      margin-bottom: 12px;
    }

    .code-badge {
      font-family: 'JetBrains Mono', ui-monospace, monospace;
      font-size: 14px;
      font-weight: 700;
      padding: 4px 10px;
      border-radius: 6px;
      background: var(--color-neutral-100, #f5f5f5);
      color: var(--color-neutral-900, #212121);
      white-space: nowrap;
    }

    .code-badge--rejected {
      text-decoration: line-through;
      color: var(--color-neutral-500, #9e9e9e);
    }

    .suggestion-card__description {
      font-size: 14px;
      color: var(--color-neutral-800, #424242);
    }

    .suggestion-card__description--rejected {
      text-decoration: line-through;
      color: var(--color-neutral-500, #9e9e9e);
    }

    .suggestion-card__confidence {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 12px;
    }

    .confidence-label {
      font-size: 12px;
      font-weight: 500;
      color: var(--color-neutral-600, #757575);
      white-space: nowrap;
    }

    mat-progress-bar { flex: 1; }

    .confidence-value {
      font-size: 12px;
      font-weight: 600;
      color: var(--color-neutral-800, #424242);
      min-width: 36px;
      text-align: right;
    }

    .suggestion-card__rationale {
      font-size: 13px;
      line-height: 1.5;
      color: var(--color-neutral-700, #616161);
      margin: 0;
    }

    .btn-accept {
      background-color: #2e7d32;
      color: #fff;
    }
  `],
})
export class SuggestionCardComponent {
  readonly suggestion = input.required<IcdSuggestionDto>();
  readonly viewEvidence = output<IcdSuggestionDto>();

  protected readonly decisionFacade = inject(CodingDecisionFacade);
  private readonly dialog = inject(MatDialog);

  /** Whether the inline edit form is currently open. */
  readonly editingActive = signal(false);

  /** Code / description pre-populated in the inline edit form. */
  readonly editingCode = signal('');
  readonly editingDescription = signal('');

  /** Resolved display state combining facade state and local editing flag. */
  readonly resolvedState = () => {
    if (this.editingActive()) {
      return 'editing';
    }
    return this.decisionFacade.decisions()[this.suggestion().decisionId]?.state ?? 'pending';
  };

  protected onAccept(): void {
    this.decisionFacade.accept(
      this.suggestion().decisionId,
      this.suggestion().icdCode,
      this.suggestion().description,
    );
  }

  protected onModifyClick(): void {
    const entry = this.decisionFacade.decisions()[this.suggestion().decisionId];
    this.editingCode.set(entry?.finalCode ?? this.suggestion().icdCode);
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

  /** Edge Case 1: re-open inline edit with the previously accepted / modified code. */
  protected onEditDecision(): void {
    const entry = this.decisionFacade.decisions()[this.suggestion().decisionId];
    this.editingCode.set(entry?.finalCode ?? this.suggestion().icdCode);
    this.editingDescription.set(entry?.finalDescription ?? this.suggestion().description);
    this.editingActive.set(true);
  }
}
