import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';

import type { CodeResultDto } from '../../../../shared/models/code-search.dto';

/**
 * Single code search result row (SCR-018 / US_052).
 *
 * Renders inside both `mat-option` (autocomplete dropdown) and the
 * `FavoritesSectionComponent` list. The star `mat-icon-button` calls
 * `$event.stopPropagation()` so clicking it does not trigger parent
 * `mat-option` selection.
 *
 * Accessibility:
 * - Star button `aria-label` reflects add/remove intent (UXR-304).
 * - Touch target is at least 44×44 px via CSS (UXR-304).
 * - Code badge rendered in JetBrains Mono for readability.
 */
@Component({
  selector: 'app-code-result-item',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatButtonModule, MatChipsModule],
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: 8px;
      width: 100%;
      box-sizing: border-box;
    }

    .code-badge {
      font-family: 'JetBrains Mono', 'Courier New', monospace;
      font-size: 0.8rem;
      font-weight: 600;
      background: var(--mat-sys-surface-variant, #e7e0ec);
      color: var(--mat-sys-on-surface-variant, #49454f);
      border-radius: 4px;
      padding: 2px 6px;
      white-space: nowrap;
      flex-shrink: 0;
    }

    .code-result-item__content {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-width: 0;
      gap: 2px;
    }

    .code-result-item__description {
      font-size: 0.875rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .code-result-item__chips {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
    }

    .type-chip {
      font-size: 0.7rem;
      min-height: 20px;
      height: 20px;
    }

    .type-chip--cpt {
      --mat-chip-label-text-color: var(--mat-sys-on-tertiary-container, #1d1b20);
      background-color: var(--mat-sys-tertiary-container, #ffd8e4);
    }

    .deprecated-chip {
      font-size: 0.7rem;
      min-height: 20px;
      height: 20px;
      background-color: var(--mat-sys-error-container, #ffdad6);
      --mat-chip-label-text-color: var(--mat-sys-on-error-container, #410002);
    }

    .code-result-item__favorite-btn {
      flex-shrink: 0;
      min-width: 44px;
      min-height: 44px;
      color: var(--mat-sys-secondary, #625b71);
    }

    .code-result-item__favorite-btn--active {
      color: var(--mat-sys-primary, #6750a4);
    }
  `,
  template: `
    <span class="code-badge" aria-hidden="true">{{ result().code }}</span>
    <div class="code-result-item__content">
      <span class="code-result-item__description" [title]="result().description">
        {{ result().description }}
      </span>
      <div class="code-result-item__chips">
        <mat-chip
          class="type-chip"
          [class.type-chip--cpt]="result().codeType === 'cpt'"
          disableRipple
          highlighted>
          {{ result().codeType === 'icd10' ? 'ICD-10' : 'CPT' }}
        </mat-chip>
        @if (result().isDeprecated) {
          <mat-chip class="deprecated-chip" disableRipple highlighted>Inactive</mat-chip>
        }
      </div>
    </div>
    <button
      mat-icon-button
      class="code-result-item__favorite-btn"
      [class.code-result-item__favorite-btn--active]="isFavorited()"
      [attr.aria-label]="isFavorited() ? 'Remove ' + result().code + ' from favorites' : 'Add ' + result().code + ' to favorites'"
      (click)="onFavoriteClick($event)"
      type="button">
      <mat-icon>{{ isFavorited() ? 'star' : 'star_border' }}</mat-icon>
    </button>
  `,
})
export class CodeResultItemComponent {
  readonly result = input.required<CodeResultDto>();
  readonly isFavorited = input<boolean>(false);

  readonly favoriteToggled = output<CodeResultDto>();

  protected onFavoriteClick(event: MouseEvent): void {
    event.stopPropagation();
    this.favoriteToggled.emit(this.result());
  }
}
