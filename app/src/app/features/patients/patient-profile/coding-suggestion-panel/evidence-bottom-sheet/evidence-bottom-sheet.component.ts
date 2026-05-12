import {
  ChangeDetectionStrategy,
  Component,
  inject,
} from '@angular/core';
import { MAT_BOTTOM_SHEET_DATA, MatBottomSheetRef } from '@angular/material/bottom-sheet';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { A11yModule } from '@angular/cdk/a11y';
import { DatePipe } from '@angular/common';

import type { ClinicalFactCitationDto } from '../../../../../shared/models/coding-suggestion.model';

/**
 * Bottom sheet listing supporting clinical fact citations for a suggestion (AC-2).
 *
 * Opened by CodingSuggestionPanelComponent when the user clicks "View Evidence".
 * Uses cdkTrapFocus for accessibility; provides a close button to dismiss.
 */
@Component({
  selector: 'app-evidence-bottom-sheet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatDividerModule,
    A11yModule,
    DatePipe,
  ],
  template: `
    <div class="evidence-sheet" cdkTrapFocus>
      <div class="evidence-sheet__header">
        <h3 class="evidence-sheet__title">
          <mat-icon aria-hidden="true">fact_check</mat-icon>
          Supporting Evidence
        </h3>
        <button
          mat-icon-button
          type="button"
          aria-label="Close evidence panel"
          (click)="close()"
        >
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <mat-divider />

      @if (citations.length === 0) {
        <p class="evidence-sheet__empty">No supporting citations available.</p>
      } @else {
        <mat-list role="list">
          @for (citation of citations; track citation.factId) {
            <mat-list-item role="listitem">
              <mat-icon matListItemIcon aria-hidden="true">description</mat-icon>
              <span matListItemTitle class="citation-name">{{ citation.name }}</span>
              <span matListItemLine>
                <span class="citation-type">{{ citation.factType }}</span>
                <span class="citation-separator" aria-hidden="true">&middot;</span>
                <span class="citation-value">{{ citation.value }}</span>
                @if (citation.factDate) {
                  <span class="citation-separator" aria-hidden="true">&middot;</span>
                  <span class="citation-date">{{ citation.factDate | date:'mediumDate' }}</span>
                }
              </span>
            </mat-list-item>
          }
        </mat-list>
      }
    </div>
  `,
  styles: [`
    .evidence-sheet { padding: 16px; }

    .evidence-sheet__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 8px;
    }

    .evidence-sheet__title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 16px;
      font-weight: 600;
      margin: 0;
      color: var(--color-neutral-900, #212121);
      mat-icon { color: var(--color-neutral-600, #757575); }
    }

    .evidence-sheet__empty {
      padding: 24px 0;
      text-align: center;
      color: var(--color-neutral-600, #757575);
    }

    .citation-name { font-weight: 500; }

    .citation-type {
      text-transform: capitalize;
      font-size: 12px;
      color: var(--color-neutral-600, #757575);
    }

    .citation-separator {
      margin: 0 4px;
      color: var(--color-neutral-400, #bdbdbd);
    }

    .citation-value {
      font-size: 12px;
      color: var(--color-neutral-700, #616161);
    }

    .citation-date {
      font-size: 12px;
      color: var(--color-neutral-500, #9e9e9e);
    }
  `],
})
export class EvidenceBottomSheetComponent {
  readonly citations = inject<ClinicalFactCitationDto[]>(MAT_BOTTOM_SHEET_DATA);
  private readonly bottomSheetRef = inject(MatBottomSheetRef<EvidenceBottomSheetComponent>);

  close(): void {
    this.bottomSheetRef.dismiss();
  }
}
