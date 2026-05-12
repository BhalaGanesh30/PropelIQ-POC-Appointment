import {
  DestroyRef,
  Injectable,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, EMPTY } from 'rxjs';
import { catchError, debounceTime, switchMap } from 'rxjs/operators';

import { CodeSearchService } from './code-search.service';
import type {
  CodeFavoriteDto,
  CodeResultDto,
} from '../../shared/models/code-search.dto';

export type CodeSearchLoadingState = 'idle' | 'loading' | 'loaded' | 'empty' | 'error';

/**
 * Signal-based facade for code search and favorites (US_052 / SCR-018).
 *
 * Provided at the CodeSearchComponent level so state resets on unmount.
 *
 * Search debounce: 300 ms via a Subject + debounceTime + switchMap.
 * Favorites: optimistic update pattern — revert + snackbar on API error.
 */
@Injectable()
export class CodeSearchFacade {
  private readonly service = inject(CodeSearchService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly query = signal('');
  readonly results = signal<CodeResultDto[]>([]);
  readonly favorites = signal<CodeFavoriteDto[]>([]);
  readonly loadingState = signal<CodeSearchLoadingState>('idle');
  readonly includeDeprecated = signal(false);
  readonly selectedCode = signal<CodeResultDto | null>(null);

  /**
   * Set of `{codeType}:{code}` keys for O(1) favorite lookups in templates.
   */
  readonly favoritedCodes = computed(() =>
    new Set(this.favorites().map((f) => `${f.codeType}:${f.code}`)),
  );

  private readonly searchTrigger$ = new Subject<void>();

  constructor() {
    this.searchTrigger$
      .pipe(
        debounceTime(300),
        switchMap(() => {
          const q = this.query();
          if (q.length < 2) {
            this.results.set([]);
            this.loadingState.set('idle');
            return EMPTY;
          }
          this.loadingState.set('loading');
          return this.service
            .search(q, 'all', this.includeDeprecated())
            .pipe(
              catchError(() => {
                this.loadingState.set('error');
                return EMPTY;
              }),
            );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((response) => {
        this.results.set(response.results);
        this.loadingState.set(
          response.results.length === 0 ? 'empty' : 'loaded',
        );
      });
  }

  /**
   * Dispatch a debounced search using the current query and includeDeprecated
   * signals. Clears results immediately when query is shorter than 2 characters
   * (UXR-506 AC-1).
   */
  triggerSearch(): void {
    if (this.query().length < 2) {
      this.results.set([]);
      this.loadingState.set('idle');
      return;
    }
    this.searchTrigger$.next();
  }

  loadFavorites(): void {
    this.service
      .getFavorites()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (favs) => this.favorites.set(favs),
      });
  }

  /**
   * Optimistically toggle the favorite state of a code result (AC-3, AC-4).
   * On API failure, reverts to the previous state and shows an error snackbar.
   */
  toggleFavorite(item: CodeResultDto): void {
    const key = `${item.codeType}:${item.code}`;
    const isCurrentlyFav = this.favoritedCodes().has(key);
    const prev = this.favorites();

    if (isCurrentlyFav) {
      this.favorites.update((favs) =>
        favs.filter((f) => `${f.codeType}:${f.code}` !== key),
      );
      this.service
        .removeFavorite(item.codeType, item.code)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          error: () => {
            this.favorites.set(prev);
            this.snackBar.open(
              'Failed to remove favorite. Please try again.',
              'Close',
              { duration: 4000 },
            );
          },
        });
    } else {
      this.favorites.update((favs) => [
        ...favs,
        { code: item.code, description: item.description, codeType: item.codeType },
      ]);
      this.service
        .addFavorite({ code: item.code, codeType: item.codeType })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          error: () => {
            this.favorites.set(prev);
            this.snackBar.open(
              'Failed to add favorite. Please try again.',
              'Close',
              { duration: 4000 },
            );
          },
        });
    }
  }
}
