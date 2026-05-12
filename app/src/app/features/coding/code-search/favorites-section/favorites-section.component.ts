import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
} from '@angular/core';
import { BreakpointObserver } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { map } from 'rxjs/operators';

import type { CodeFavoriteDto } from '../../../../shared/models/code-search.dto';

/**
 * Favorites sidebar / collapsible panel (SCR-018 / US_052).
 *
 * Desktop (≥ 960px): renders as an inline sidebar visible below the search field.
 * Mobile  (< 960px): renders as a `mat-expansion-panel` the user can collapse.
 *
 * Emits:
 * - `favoriteCodeSelected`: when the user clicks a favorited code row.
 * - `favoriteToggled`: when the user removes a code from favorites via the star.
 */
@Component({
  selector: 'app-favorites-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatExpansionModule, MatListModule, MatIconModule, MatButtonModule],
  styles: `
    .favorites-section {
      width: 100%;
    }

    .favorites-section__header {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 0 8px;
      font-size: 0.875rem;
      font-weight: 600;
      color: var(--mat-sys-on-surface-variant, #49454f);
    }

    .favorites-section__empty {
      padding: 12px 0;
      font-size: 0.875rem;
      color: var(--mat-sys-on-surface-variant, #49454f);
      font-style: italic;
    }

    .favorites-section__list {
      padding: 0;
    }

    .favorite-item {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
      border-radius: 4px;
    }

    .favorite-item:hover {
      background: var(--mat-sys-surface-variant, #e7e0ec);
    }

    .favorite-item__code {
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

    .favorite-item__description {
      flex: 1;
      font-size: 0.875rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .favorite-item__remove-btn {
      flex-shrink: 0;
      min-width: 44px;
      min-height: 44px;
      color: var(--mat-sys-primary, #6750a4);
    }

    .desktop-header {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 4px;
    }

    .desktop-header mat-icon {
      color: var(--mat-sys-primary, #6750a4);
    }
  `,
  template: `
    @if (isDesktop()) {
      <!-- Desktop: inline sidebar -->
      <div class="favorites-section" role="region" aria-label="Your favorite codes">
        <div class="desktop-header">
          <mat-icon aria-hidden="true">star</mat-icon>
          <span class="favorites-section__header">Your Favorites</span>
        </div>
        @if (favorites().length === 0) {
          <p class="favorites-section__empty">No favorites yet. Use the ★ icon to save codes for quick access.</p>
        } @else {
          <mat-list class="favorites-section__list" role="list">
            @for (fav of favorites(); track fav.codeType + ':' + fav.code) {
              <mat-list-item
                class="favorite-item"
                role="listitem"
                (click)="onFavoriteSelected(fav)"
                [attr.aria-label]="fav.code + ': ' + fav.description">
                <span class="favorite-item__code" aria-hidden="true">{{ fav.code }}</span>
                <span class="favorite-item__description" [title]="fav.description">{{ fav.description }}</span>
                <button
                  mat-icon-button
                  class="favorite-item__remove-btn"
                  [attr.aria-label]="'Remove ' + fav.code + ' from favorites'"
                  (click)="onRemoveFavorite($event, fav)"
                  type="button">
                  <mat-icon>star</mat-icon>
                </button>
              </mat-list-item>
            }
          </mat-list>
        }
      </div>
    } @else {
      <!-- Mobile: collapsible expansion panel -->
      <mat-expansion-panel class="favorites-section">
        <mat-expansion-panel-header>
          <mat-panel-title>
            <mat-icon aria-hidden="true">star</mat-icon>
            Your Favorites
            @if (favorites().length > 0) {
              <span class="mat-badge-content" [attr.aria-label]="favorites().length + ' favorites'">
                &nbsp;({{ favorites().length }})
              </span>
            }
          </mat-panel-title>
        </mat-expansion-panel-header>

        @if (favorites().length === 0) {
          <p class="favorites-section__empty">No favorites yet. Use the ★ icon to save codes for quick access.</p>
        } @else {
          <mat-list class="favorites-section__list" role="list">
            @for (fav of favorites(); track fav.codeType + ':' + fav.code) {
              <mat-list-item
                class="favorite-item"
                role="listitem"
                (click)="onFavoriteSelected(fav)"
                [attr.aria-label]="fav.code + ': ' + fav.description">
                <span class="favorite-item__code" aria-hidden="true">{{ fav.code }}</span>
                <span class="favorite-item__description" [title]="fav.description">{{ fav.description }}</span>
                <button
                  mat-icon-button
                  class="favorite-item__remove-btn"
                  [attr.aria-label]="'Remove ' + fav.code + ' from favorites'"
                  (click)="onRemoveFavorite($event, fav)"
                  type="button">
                  <mat-icon>star</mat-icon>
                </button>
              </mat-list-item>
            }
          </mat-list>
        }
      </mat-expansion-panel>
    }
  `,
})
export class FavoritesSectionComponent {
  readonly favorites = input.required<CodeFavoriteDto[]>();

  readonly favoriteCodeSelected = output<CodeFavoriteDto>();
  /**
   * Emitted when the user removes a favorite using the star button on a
   * `CodeFavoriteDto` row. The host converts the dto to a partial `CodeResultDto`
   * and calls `facade.toggleFavorite()`.
   */
  readonly favoriteRemoved = output<CodeFavoriteDto>();

  private readonly breakpointObserver = inject(BreakpointObserver);

  protected readonly isDesktop = toSignal(
    this.breakpointObserver
      .observe('(min-width: 960px)')
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected onFavoriteSelected(fav: CodeFavoriteDto): void {
    this.favoriteCodeSelected.emit(fav);
  }

  protected onRemoveFavorite(event: MouseEvent, fav: CodeFavoriteDto): void {
    event.stopPropagation();
    this.favoriteRemoved.emit(fav);
  }
}
