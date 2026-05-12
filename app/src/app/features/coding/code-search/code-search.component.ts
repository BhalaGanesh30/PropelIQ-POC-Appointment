import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { CodeSearchFacade } from '../code-search.facade';
import { CodeResultItemComponent } from './code-result-item/code-result-item.component';
import { FavoritesSectionComponent } from './favorites-section/favorites-section.component';
import type {
  CodeFavoriteDto,
  CodeResultDto,
} from '../../../shared/models/code-search.dto';

/**
 * Code search screen (SCR-018 / US_052 / EP-008).
 *
 * Provides a `MatAutocomplete`-based code lookup for ICD-10 and CPT codes
 * with:
 * - 300 ms debounce via `CodeSearchFacade` (UXR-506 AC-1: ≥ 2 chars)
 * - Favorites group + results group inside the autocomplete overlay
 * - Optimistic favorites toggle (AC-3, AC-4)
 * - "Include inactive codes" slide-toggle (Edge Case 2)
 * - `FavoritesSectionComponent` below the search (sidebar on desktop, collapsible on mobile)
 *
 * When `standalone = true` (routed view at /coding/search), the component
 * emits `codeSelected` to let the parent shell record the selection.
 */
@Component({
  selector: 'app-code-search',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [CodeSearchFacade],
  imports: [
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    CodeResultItemComponent,
    FavoritesSectionComponent,
  ],
  styles: `
    :host {
      display: block;
      max-width: 720px;
      margin: 0 auto;
      padding: 24px 16px;
    }

    .code-search__title {
      font-size: 1.25rem;
      font-weight: 600;
      margin-bottom: 16px;
      color: var(--mat-sys-on-surface, #1c1b1f);
    }

    .code-search__options {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 8px;
    }

    .code-search__field {
      width: 100%;
    }

    .code-search__option-content {
      display: flex;
      align-items: center;
      width: 100%;
      padding: 0;
    }

    .code-search__loading-option {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--mat-sys-on-surface-variant, #49454f);
      font-size: 0.875rem;
      padding: 8px 0;
    }

    .code-search__status-option {
      font-size: 0.875rem;
      color: var(--mat-sys-on-surface-variant, #49454f);
      padding: 8px 0;
      white-space: normal;
    }

    .code-search__retry-btn {
      margin-left: 4px;
    }

    .code-search__divider {
      margin: 16px 0 8px;
    }
  `,
  template: `
    <h1 class="code-search__title">Code Search</h1>

    <!-- Include inactive toggle (Edge Case 2) -->
    <div class="code-search__options">
      <mat-slide-toggle
        [checked]="facade.includeDeprecated()"
        (change)="onToggleDeprecated($event.checked)"
        aria-label="Include inactive / deprecated codes in results">
        Include inactive codes
      </mat-slide-toggle>
    </div>

    <!-- Search input -->
    <mat-form-field appearance="outline" class="code-search__field">
      <mat-label>Search ICD-10 &amp; CPT codes</mat-label>
      <mat-icon matPrefix aria-hidden="true">search</mat-icon>
      <input
        matInput
        [formControl]="searchControl"
        [matAutocomplete]="auto"
        placeholder="Type at least 2 characters…"
        aria-label="Search ICD-10 and CPT codes"
        aria-autocomplete="list"
        autocomplete="off" />
      @if (facade.loadingState() === 'loading') {
        <mat-progress-spinner
          matSuffix
          mode="indeterminate"
          diameter="20"
          aria-label="Searching codes…" />
      }
    </mat-form-field>

    <!-- Autocomplete panel -->
    <mat-autocomplete
      #auto="matAutocomplete"
      [displayWith]="displayFn"
      (optionSelected)="onOptionSelected($event)">

      <!-- Favorites group (query ≥ 2 chars, matching favorites) -->
      @if (matchingFavorites().length > 0) {
        <mat-optgroup label="Your Favorites">
          @for (item of matchingFavorites(); track item.codeType + ':' + item.code) {
            <mat-option [value]="item" class="code-search__option-content">
              <app-code-result-item
                [result]="item"
                [isFavorited]="true"
                (favoriteToggled)="facade.toggleFavorite($event)" />
            </mat-option>
          }
        </mat-optgroup>
      }

      <!-- Results / state options -->
      @switch (facade.loadingState()) {
        @case ('idle') {
          <mat-option disabled>
            <span class="code-search__status-option">
              Start typing to search codes
            </span>
          </mat-option>
        }
        @case ('loading') {
          <mat-option disabled>
            <span class="code-search__loading-option">
              <mat-progress-spinner
                mode="indeterminate"
                diameter="16"
                aria-hidden="true" />
              Searching…
            </span>
          </mat-option>
        }
        @case ('empty') {
          <mat-option disabled>
            <span class="code-search__status-option">
              No codes found for your search term. Try a different keyword or code number.
            </span>
          </mat-option>
        }
        @case ('error') {
          <mat-option disabled>
            <span class="code-search__status-option">
              Search unavailable.
              <button
                mat-button
                color="primary"
                class="code-search__retry-btn"
                type="button"
                (click)="onRetry($event)">
                Retry
              </button>
            </span>
          </mat-option>
        }
        @case ('loaded') {
          @if (facade.results().length > 0) {
            <mat-optgroup label="Results">
              @for (item of facade.results(); track item.codeType + ':' + item.code) {
                <mat-option [value]="item" class="code-search__option-content">
                  <app-code-result-item
                    [result]="item"
                    [isFavorited]="facade.favoritedCodes().has(item.codeType + ':' + item.code)"
                    (favoriteToggled)="facade.toggleFavorite($event)" />
                </mat-option>
              }
            </mat-optgroup>
          }
        }
      }
    </mat-autocomplete>

    <!-- Favorites section (sidebar on desktop / collapsible on mobile) -->
    <div class="code-search__divider" aria-hidden="true"></div>
    <app-favorites-section
      [favorites]="facade.favorites()"
      (favoriteCodeSelected)="onFavoriteCodeSelected($event)"
      (favoriteRemoved)="onFavoriteRemoved($event)" />
  `,
})
export class CodeSearchComponent implements OnInit {
  /** When true (routed /coding/search view), emits codeSelected for the host shell. */
  readonly standalone = input<boolean>(true);

  /** Emitted when the clinician selects a code from the dropdown or favorites. */
  readonly codeSelected = output<CodeResultDto>();

  protected readonly facade = inject(CodeSearchFacade);
  private readonly destroyRef = inject(DestroyRef);

  readonly searchControl = new FormControl<string | CodeResultDto>('');

  /**
   * Favorites that match the current query — shown in the "Your Favorites" optgroup
   * so clinicians can quickly re-select previously saved codes (UXR-506).
   */
  protected readonly matchingFavorites = computed((): CodeResultDto[] => {
    const q = this.facade.query().toLowerCase().trim();
    if (q.length < 2) {
      return [];
    }
    return this.facade
      .favorites()
      .filter(
        (f) =>
          f.code.toLowerCase().includes(q) ||
          f.description.toLowerCase().includes(q),
      )
      .map((f) => ({
        code: f.code,
        description: f.description,
        codeType: f.codeType,
        isDeprecated: false,
        isFavorited: true,
      }));
  });

  ngOnInit(): void {
    this.facade.loadFavorites();

    this.searchControl.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((val) => {
        if (typeof val === 'string') {
          this.facade.query.set(val);
          this.facade.triggerSearch();
        }
        // When val is CodeResultDto (post-selection), displayFn handles display;
        // we skip re-triggering a search.
      });
  }

  /** Converts a `CodeResultDto` to a display string for the autocomplete input. */
  protected readonly displayFn = (item: CodeResultDto | string | null): string => {
    if (!item) {
      return '';
    }
    if (typeof item === 'string') {
      return item;
    }
    return `${item.code} — ${item.description}`;
  };

  protected onOptionSelected(event: MatAutocompleteSelectedEvent): void {
    const item = event.option.value as CodeResultDto;
    this.facade.selectedCode.set(item);
    this.codeSelected.emit(item);
  }

  protected onToggleDeprecated(checked: boolean): void {
    this.facade.includeDeprecated.set(checked);
    this.facade.triggerSearch();
  }

  protected onRetry(event: MouseEvent): void {
    event.stopPropagation();
    this.facade.triggerSearch();
  }

  protected onFavoriteCodeSelected(fav: CodeFavoriteDto): void {
    const item: CodeResultDto = {
      code: fav.code,
      description: fav.description,
      codeType: fav.codeType,
      isDeprecated: false,
      isFavorited: true,
    };
    this.facade.selectedCode.set(item);
    this.codeSelected.emit(item);
  }

  protected onFavoriteRemoved(fav: CodeFavoriteDto): void {
    this.facade.toggleFavorite({
      code: fav.code,
      description: fav.description,
      codeType: fav.codeType,
      isDeprecated: false,
      isFavorited: true,
    });
  }
}
