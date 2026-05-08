import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import type {
  ClinicalFactDto,
  ConcurrencyConflictResponseDto,
} from '../../../../shared/models/clinical-fact.model';
import { ClinicalFactService } from '../../clinical-fact.service';
import { TokenStorageService } from '../../../../core/services/token-storage.service';

import { FactEditFormComponent, type FactEditSubmitEvent } from '../fact-editing/fact-edit-form.component';
import { ConcurrencyConflictBannerComponent } from '../fact-editing/concurrency-conflict-banner.component';
import { CodingDecisionWarningComponent } from '../fact-editing/coding-decision-warning.component';
import { FactHistoryPanelComponent } from '../fact-editing/fact-history-panel.component';

/**
 * Card representing a single extracted clinical fact (AIR-004 / UXR-107).
 *
 * Clinician-only actions (US_047):
 *  - Edit: inline Reactive Form for name + value with ETag concurrency.
 *  - Verify: one-click badge transition to green Verified state.
 *  - History: lazy-loading audit history expansion panel (AC-3).
 */
@Component({
  selector: 'app-clinical-fact-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatIconModule,
    MatTooltipModule,
    MatChipsModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    FactEditFormComponent,
    ConcurrencyConflictBannerComponent,
    CodingDecisionWarningComponent,
    FactHistoryPanelComponent,
  ],
  template: `
    <article
      class="fact-card"
      [class.fact-card--needs-review]="fact().needsReview"
      [class.fact-card--editing]="editMode()"
      [attr.aria-label]="'Clinical fact: ' + (localFact().name ?? localFact().value)"
    >
      <!-- ── Badges row ─────────────────────────────────────────── -->
      <div class="fact-card__badges">
        @if (localFact().verified) {
          <span
            class="badge badge--verified"
            aria-label="Verified by clinician"
            matTooltip="Verified by clinician"
          >
            <mat-icon aria-hidden="true">verified</mat-icon>
            Verified
          </span>
        }
        @if (!localFact().verified) {
          <span class="badge badge--ai" aria-label="AI-extracted">
            <mat-icon aria-hidden="true">smart_toy</mat-icon>
            AI
          </span>
        }
        @if (localFact().needsReview) {
          <span class="badge badge--review" aria-label="Needs review">
            <mat-icon aria-hidden="true">rate_review</mat-icon>
            Review
          </span>
        }
      </div>

      @if (!editMode()) {
        <!-- ── View mode ───────────────────────────────────────────── -->
        <div class="fact-card__content">
          <p class="fact-card__name">{{ localFact().name ?? localFact().value }}</p>
          @if (localFact().name && localFact().value !== localFact().name) {
            <p class="fact-card__value">{{ localFact().value }}</p>
          }
          @if (localFact().factDate) {
            <p class="fact-card__date">{{ localFact().factDate | date:'mediumDate' }}</p>
          }
        </div>

        <!-- ── Clinician-only actions ─────────────────────────────── -->
        @if (canEdit()) {
          <div class="fact-card__actions" role="group" [attr.aria-label]="'Actions for ' + (localFact().name ?? localFact().value)">
            <button
              mat-icon-button
              type="button"
              class="fact-card__action-btn"
              [attr.aria-label]="'Edit fact: ' + (localFact().name ?? localFact().value)"
              (click)="enterEditMode()"
              matTooltip="Edit fact"
            >
              <mat-icon>edit</mat-icon>
            </button>
            @if (!localFact().verified) {
              <button
                mat-icon-button
                type="button"
                class="fact-card__action-btn fact-card__action-btn--verify"
                [attr.aria-label]="'Verify fact: ' + (localFact().name ?? localFact().value)"
                [disabled]="verifying()"
                (click)="verify()"
                matTooltip="Verify fact"
              >
                @if (verifying()) {
                  <mat-spinner diameter="18" strokeWidth="2" />
                } @else {
                  <mat-icon>verified</mat-icon>
                }
              </button>
            }
          </div>
        }

        <!-- ── Source traceability ────────────────────────────────── -->
        @if (localFact().documentDisplayName) {
          <button
            class="fact-card__source-btn"
            type="button"
            [matTooltip]="sourceTooltip()"
            matTooltipPosition="above"
            aria-label="View source document details"
          >
            <mat-icon aria-hidden="true">description</mat-icon>
            <span class="fact-card__source-name">{{ localFact().documentDisplayName }}</span>
            <span class="fact-card__confidence">{{ (localFact().confidenceScore * 100).toFixed(0) }}%</span>
          </button>
        }
      } @else {
        <!-- ── Edit mode ───────────────────────────────────────────── -->

        @if (showCodingWarning()) {
          <app-coding-decision-warning />
        }

        @if (conflictCurrentValue()) {
          <app-concurrency-conflict-banner [currentValue]="conflictCurrentValue()!" />
        }

        <app-fact-edit-form
          [fact]="localFact()"
          [saving]="saving()"
          (submitted)="onEditSubmit($event)"
          (cancelled)="exitEditMode()"
        />
      }

      <!-- ── Inline error (non-409) ─────────────────────────────── -->
      @if (saveError()) {
        <p class="fact-card__error" role="alert" aria-live="polite">
          <mat-icon aria-hidden="true">error_outline</mat-icon>
          {{ saveError() }}
        </p>
      }

      <!-- ── History panel (lazy) ────────────────────────────────── -->
      <app-fact-history-panel
        [factId]="localFact().factId"
        [factName]="localFact().name ?? localFact().value"
      />
    </article>
  `,
  styles: [`
    .fact-card {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 12px 16px;
      background: #fff;
      border: 1px solid var(--color-neutral-200, #e0e0e0);
      border-radius: 8px;
      transition: box-shadow 0.15s ease;

      &:hover { box-shadow: 0 2px 8px rgba(0,0,0,.08); }
    }

    .fact-card.fact-card--needs-review { border-left: 3px solid #f57c00; }
    .fact-card.fact-card--editing { border-color: #1976d2; }

    .fact-card__badges {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
    }

    .badge {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      font-size: 11px;
      font-weight: 600;
      padding: 2px 7px;
      border-radius: 12px;
      line-height: 18px;

      mat-icon { font-size: 13px; width: 13px; height: 13px; }
    }

    .badge--ai      { background: #ede7f6; color: #6a1b9a; }
    .badge--verified { background: #e8f5e9; color: #2e7d32; }
    .badge--review  { background: #fff3e0; color: #e65100; }

    .fact-card__content {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .fact-card__name {
      font-size: 14px;
      font-weight: 600;
      color: var(--color-neutral-900, #212121);
      margin: 0;
    }

    .fact-card__value {
      font-size: 13px;
      color: var(--color-neutral-700, #616161);
      margin: 0;
    }

    .fact-card__date {
      font-size: 12px;
      color: var(--color-neutral-500, #9e9e9e);
      margin: 0;
    }

    .fact-card__actions {
      display: flex;
      gap: 4px;
    }

    .fact-card__action-btn {
      color: var(--color-neutral-600, #757575);

      &:focus-visible {
        outline: 2px solid #1976d2;
        outline-offset: 2px;
        border-radius: 50%;
      }
    }

    .fact-card__action-btn--verify {
      color: #2e7d32;
    }

    .fact-card__source-btn {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      background: none;
      border: none;
      padding: 4px 0;
      cursor: pointer;
      font-size: 12px;
      color: var(--color-neutral-600, #757575);
      border-radius: 4px;

      &:focus-visible {
        outline: 2px solid #1976d2;
        outline-offset: 2px;
      }

      mat-icon { font-size: 14px; width: 14px; height: 14px; }
    }

    .fact-card__source-name {
      max-width: 160px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .fact-card__confidence {
      font-weight: 600;
      color: #1976d2;
    }

    .fact-card__error {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 12px;
      color: #c62828;
      margin: 0;

      mat-icon { font-size: 16px; width: 16px; height: 16px; }
    }
  `],
})
export class ClinicalFactCardComponent {
  readonly fact = input.required<ClinicalFactDto>();

  /** Emitted after a successful edit or verify so the parent facade can update its Signal. */
  readonly factUpdated = output<ClinicalFactDto>();

  private readonly factService = inject(ClinicalFactService);
  private readonly tokenStorage = inject(TokenStorageService);

  // ── Role guard ────────────────────────────────────────────────────────────
  protected readonly canEdit = computed(
    () => this.tokenStorage.getUserRole() === 'Clinician',
  );

  // ── Local mutable state ───────────────────────────────────────────────────
  /** Local copy of the fact — updated optimistically after edits/verifies. */
  protected readonly localFact = signal<ClinicalFactDto>(this.fact());

  protected readonly editMode = signal(false);
  protected readonly saving = signal(false);
  protected readonly verifying = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly conflictCurrentValue = signal<string | null>(null);
  protected readonly showCodingWarning = signal(false);

  ngOnInit(): void {
    // Sync localFact with parent input on init.
    this.localFact.set(this.fact());
  }

  // ── Edit mode ─────────────────────────────────────────────────────────────
  protected enterEditMode(): void {
    this.editMode.set(true);
    this.saveError.set(null);
    this.conflictCurrentValue.set(null);
    this.showCodingWarning.set(false);
  }

  protected exitEditMode(): void {
    this.editMode.set(false);
    this.saveError.set(null);
    this.conflictCurrentValue.set(null);
    this.showCodingWarning.set(false);
  }

  protected onEditSubmit(event: FactEditSubmitEvent): void {
    this.saving.set(true);
    this.saveError.set(null);
    this.conflictCurrentValue.set(null);

    this.factService.patchFact(event.factId, event.dto, event.etag).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.localFact.set(updated);

        if (updated.referencedByCodingDecision) {
          this.showCodingWarning.set(true);
        }

        this.editMode.set(false);
        this.factUpdated.emit(updated);
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);

        if (err.status === 409) {
          // Optimistic concurrency conflict — Edge Case 1.
          const body = err.error as ConcurrencyConflictResponseDto;
          this.conflictCurrentValue.set(body?.currentName ?? body?.currentValue ?? '');
        } else {
          this.saveError.set('Failed to save. Please try again.');
        }
      },
    });
  }

  // ── Verify ────────────────────────────────────────────────────────────────
  protected verify(): void {
    this.verifying.set(true);
    this.saveError.set(null);

    this.factService.verifyFact(this.localFact().factId).subscribe({
      next: (updated) => {
        this.verifying.set(false);
        this.localFact.set(updated);
        this.factUpdated.emit(updated);
      },
      error: () => {
        this.verifying.set(false);
        this.saveError.set('Failed to verify. Please try again.');
      },
    });
  }

  // ── Source tooltip ────────────────────────────────────────────────────────
  protected sourceTooltip(): string {
    const f = this.localFact();
    const date = f.documentUploadedAt
      ? new Date(f.documentUploadedAt).toLocaleDateString()
      : 'Unknown date';
    const confidence = (f.confidenceScore * 100).toFixed(0);
    return `${f.documentDisplayName ?? 'Unknown document'}\nUploaded: ${date}\nConfidence: ${confidence}%`;
  }
}

