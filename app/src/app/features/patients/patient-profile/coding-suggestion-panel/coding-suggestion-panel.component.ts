import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  effect,
  inject,
  input,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatBottomSheet } from '@angular/material/bottom-sheet';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { CodingSuggestionFacade } from '../../coding-suggestion.facade';
import { CptSuggestionFacade } from '../../cpt-suggestion.facade';
import { CodingDecisionFacade } from '../../coding-decision.facade';
import { AiGatewayStatusFacade } from '../../../../shared/facades/ai-gateway-status.facade';
import { AiFallbackBannerComponent } from '../../../../shared/components/ai-fallback-banner/ai-fallback-banner.component';
import { SuggestionCardComponent } from './suggestion-card/suggestion-card.component';
import { EvidenceBottomSheetComponent } from './evidence-bottom-sheet/evidence-bottom-sheet.component';
import { LowConfidenceBannerComponent } from './low-confidence-banner/low-confidence-banner.component';
import { InsufficientEvidenceNoteComponent } from './insufficient-evidence-note/insufficient-evidence-note.component';
import { CptSuggestionCardComponent } from './cpt-suggestion-card/cpt-suggestion-card.component';
import { EmLevelCardComponent } from './em-level-card/em-level-card.component';
import { StaleCptDatabaseBannerComponent } from './stale-cpt-database-banner/stale-cpt-database-banner.component';
import { CodingDecisionSummaryBarComponent } from './coding-decision-summary-bar/coding-decision-summary-bar.component';
import type { IcdSuggestionDto } from '../../../../shared/models/coding-suggestion.model';
import type { CptSuggestionDto } from '../../../../shared/models/cpt-suggestion.model';

/** Skeleton placeholder indices for the loading state. */
const SKELETON_INDICES = [0, 1, 2];

/**
 * Host container for the ICD-10 suggestion panel (SCR-017).
 *
 * States:
 * - idle/loading → 3 skeleton cards
 * - loaded → suggestion cards + optional low-confidence banner + optional insufficient-evidence note
 * - empty (HTTP 422) → empty state with SCR-018 navigation link
 * - error → error banner with retry button
 *
 * Facade is provided at this component level for scoped lifecycle.
 */
@Component({
  selector: 'app-coding-suggestion-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [CodingSuggestionFacade, CptSuggestionFacade, CodingDecisionFacade],
  imports: [
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    AiFallbackBannerComponent,
    SuggestionCardComponent,
    LowConfidenceBannerComponent,
    InsufficientEvidenceNoteComponent,
    CptSuggestionCardComponent,
    EmLevelCardComponent,
    StaleCptDatabaseBannerComponent,
    CodingDecisionSummaryBarComponent,
  ],
  template: `
    <!-- ── AI fallback banner (US_053, AC-2, Edge Case 2) ──────────── -->
    @if (aiStatusFacade.fallbackActive()) {
      <app-ai-fallback-banner />
    }

    <!-- ── Decision summary bar (AC-4) ─────────────────────────────── -->
    @if (decisionFacade.pendingCount() > 0 || decisionFacade.acceptedCount() > 0 || decisionFacade.modifiedCount() > 0 || decisionFacade.rejectedCount() > 0) {
      <app-coding-decision-summary-bar />
    }

    <section class="suggestion-panel" aria-labelledby="icd-panel-heading">
      <h3 id="icd-panel-heading" class="suggestion-panel__title">
        <mat-icon aria-hidden="true">code</mat-icon>
        ICD-10 Code Suggestions
      </h3>

      @switch (facade.loadingState()) {
        @case ('idle') {
          <!-- No-op: initial state before load triggers -->
        }

        @case ('loading') {
          <div class="skeleton-list" aria-label="Loading suggestions" aria-busy="true">
            @for (i of skeletonIndices; track i) {
              <mat-card class="skeleton-card" appearance="outlined">
                <mat-card-content>
                  <div class="skeleton-line skeleton-line--badge"></div>
                  <div class="skeleton-line skeleton-line--text"></div>
                  <div class="skeleton-line skeleton-line--bar"></div>
                  <div class="skeleton-line skeleton-line--text-short"></div>
                </mat-card-content>
              </mat-card>
            }
          </div>
        }

        @case ('loaded') {
          @if (facade.lowConfidence()) {
            <app-low-confidence-banner />
          }

          <div class="suggestion-list">
            @for (suggestion of facade.suggestions(); track suggestion.decisionId) {
              <app-suggestion-card
                [suggestion]="suggestion"
                (viewEvidence)="openIcdEvidence($event)"
              />
            }
          </div>

          @if (facade.suggestions().length > 0 && facade.suggestions().length < 3) {
            <app-insufficient-evidence-note />
          }
        }

        @case ('empty') {
          <div class="empty-state" role="status">
            <mat-icon class="empty-state__icon" aria-hidden="true">search_off</mat-icon>
            <p class="empty-state__message">
              No suggestions available &mdash; manual coding required.
            </p>
            <a mat-stroked-button routerLink="/coding/search" class="empty-state__link">
              <mat-icon aria-hidden="true">search</mat-icon>
              Search ICD-10 codes
            </a>
          </div>
        }

        @case ('error') {
          <div class="error-state" role="alert">
            <mat-icon class="error-state__icon" aria-hidden="true">error_outline</mat-icon>
            <p class="error-state__message">
              Failed to load coding suggestions. The AI service may be temporarily unavailable.
            </p>
            <button mat-stroked-button type="button" (click)="retry()">
              <mat-icon aria-hidden="true">refresh</mat-icon>
              Retry
            </button>
          </div>
        }
      }
    </section>

    <!-- ── CPT / E/M Section ─────────────────────────────────────────── -->
    @if (appointmentId()) {
      <section class="suggestion-panel cpt-section" aria-labelledby="cpt-panel-heading">
        <h3 id="cpt-panel-heading" class="suggestion-panel__title">
          <mat-icon aria-hidden="true">assignment</mat-icon>
          CPT &amp; E/M Suggestions
        </h3>

        @if (cptFacade.staleDatabaseWarning()) {
          <app-stale-cpt-database-banner />
        }

        @switch (cptFacade.cptLoadingState()) {
          @case ('idle') {
            <!-- No-op -->
          }

          @case ('loading') {
            <div class="skeleton-list" aria-label="Loading CPT suggestions" aria-busy="true">
              @for (i of skeletonIndices; track i) {
                <mat-card class="skeleton-card" appearance="outlined">
                  <mat-card-content>
                    <div class="skeleton-line skeleton-line--badge"></div>
                    <div class="skeleton-line skeleton-line--text"></div>
                    <div class="skeleton-line skeleton-line--bar"></div>
                    <div class="skeleton-line skeleton-line--text-short"></div>
                  </mat-card-content>
                </mat-card>
              }
            </div>
          }

          @case ('loaded') {
            @if (cptFacade.cptLowConfidence()) {
              <app-low-confidence-banner />
            }

            <div class="suggestion-list">
              @for (cpt of cptFacade.cptSuggestions(); track cpt.decisionId) {
                <app-cpt-suggestion-card
                  [suggestion]="cpt"
                  (viewEvidence)="openCptEvidence($event)"
                />
              }
            </div>

            @if (cptFacade.emSuggestion()) {
              <app-em-level-card [emSuggestion]="cptFacade.emSuggestion()!" />
            }
          }

          @case ('empty') {
            <div class="empty-state" role="status">
              <mat-icon class="empty-state__icon" aria-hidden="true">search_off</mat-icon>
              <p class="empty-state__message">
                No CPT suggestion available for this appointment type.
              </p>
              <a mat-stroked-button routerLink="/coding/search" class="empty-state__link">
                <mat-icon aria-hidden="true">search</mat-icon>
                Search CPT codes
              </a>
            </div>
          }

          @case ('error') {
            <div class="error-state" role="alert">
              <mat-icon class="error-state__icon" aria-hidden="true">error_outline</mat-icon>
              <p class="error-state__message">
                Failed to load CPT suggestions. The AI service may be temporarily unavailable.
              </p>
              <button mat-stroked-button type="button" (click)="retryCpt()">
                <mat-icon aria-hidden="true">refresh</mat-icon>
                Retry
              </button>
            </div>
          }
        }
      </section>
    }
  `,
  styles: [`
    :host { display: block; }

    .suggestion-panel { display: flex; flex-direction: column; gap: 16px; }

    .cpt-section { margin-top: 32px; }

    .suggestion-panel__title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 16px;
      font-weight: 600;
      margin: 0;
      color: var(--color-neutral-800, #424242);
      mat-icon { color: var(--color-neutral-600, #757575); }
    }

    .skeleton-list,
    .suggestion-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    /* Skeleton card shimmer */
    .skeleton-card {
      --mdc-outlined-card-container-color: var(--color-neutral-50, #fafafa);
    }

    .skeleton-line {
      height: 14px;
      border-radius: 4px;
      background: linear-gradient(90deg, #e0e0e0 25%, #f5f5f5 50%, #e0e0e0 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
    }

    .skeleton-line--badge { width: 80px; height: 20px; margin-bottom: 12px; }
    .skeleton-line--text { width: 100%; margin-bottom: 8px; }
    .skeleton-line--bar { width: 100%; height: 8px; margin-bottom: 8px; }
    .skeleton-line--text-short { width: 60%; }

    @keyframes shimmer {
      0% { background-position: 200% 0; }
      100% { background-position: -200% 0; }
    }

    /* Empty state */
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      padding: 32px;
      text-align: center;
    }

    .empty-state__icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      color: var(--color-neutral-400, #bdbdbd);
    }

    .empty-state__message {
      font-size: 14px;
      color: var(--color-neutral-600, #757575);
      margin: 0;
    }

    /* Error state */
    .error-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      padding: 32px;
      text-align: center;
    }

    .error-state__icon {
      font-size: 36px;
      width: 36px;
      height: 36px;
      color: #c62828;
    }

    .error-state__message {
      font-size: 14px;
      color: var(--color-neutral-700, #616161);
      margin: 0;
    }
  `],
})
export class CodingSuggestionPanelComponent implements OnInit {
  readonly patientId = input.required<string>();
  /** Optional appointment ID — when provided, loads CPT / E/M suggestions (US_050). */
  readonly appointmentId = input<string>('');

  protected readonly facade = inject(CodingSuggestionFacade);
  protected readonly cptFacade = inject(CptSuggestionFacade);
  protected readonly decisionFacade = inject(CodingDecisionFacade);
  protected readonly aiStatusFacade = inject(AiGatewayStatusFacade);
  protected readonly skeletonIndices = SKELETON_INDICES;
  private readonly bottomSheet = inject(MatBottomSheet);
  private readonly injector = inject(Injector);

  ngOnInit(): void {
    this.facade.loadSuggestions(this.patientId());

    if (this.appointmentId()) {
      this.cptFacade.loadCptSuggestions(this.patientId(), this.appointmentId());
    }

    // Register ICD-10 decisions in the decision facade when suggestions load (AC-4).
    effect(() => {
      const suggestions = this.facade.suggestions();
      if (suggestions.length > 0) {
        this.registerIcdDecisions(suggestions);
      }
    }, { injector: this.injector });

    // Register CPT decisions when CPT suggestions load (AC-4).
    effect(() => {
      const cptSuggestions = this.cptFacade.cptSuggestions();
      if (cptSuggestions.length > 0) {
        this.registerCptDecisions(cptSuggestions);
      }
    }, { injector: this.injector });
  }

  /** Register ICD-10 decision IDs after suggestions are loaded (called from facade effect). */
  registerIcdDecisions(suggestions: IcdSuggestionDto[]): void {
    this.decisionFacade.registerDecisions(
      suggestions.map(s => ({
        decisionId: s.decisionId,
        code: s.icdCode,
        description: s.description,
      })),
    );
  }

  /** Register CPT decision IDs after CPT suggestions are loaded. */
  registerCptDecisions(suggestions: CptSuggestionDto[]): void {
    this.decisionFacade.registerDecisions(
      suggestions.map(s => ({
        decisionId: s.decisionId,
        code: s.cptCode,
        description: s.description,
      })),
    );
  }

  protected openIcdEvidence(suggestion: IcdSuggestionDto): void {
    this.bottomSheet.open(EvidenceBottomSheetComponent, {
      data: suggestion.citations,
      ariaLabel: `Supporting evidence for ${suggestion.icdCode}`,
    });
  }

  protected openCptEvidence(suggestion: CptSuggestionDto): void {
    this.bottomSheet.open(EvidenceBottomSheetComponent, {
      data: suggestion.citations,
      ariaLabel: `Supporting evidence for ${suggestion.cptCode}`,
    });
  }

  protected retry(): void {
    this.facade.loadSuggestions(this.patientId());
  }

  protected retryCpt(): void {
    this.cptFacade.loadCptSuggestions(this.patientId(), this.appointmentId());
  }
}
