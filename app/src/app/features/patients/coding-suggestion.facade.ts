import {
  DestroyRef,
  Injectable,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { CodingSuggestionService } from './coding-suggestion.service';
import type { IcdSuggestionDto } from '../../shared/models/coding-suggestion.model';

export type CodingLoadingState = 'idle' | 'loading' | 'loaded' | 'error' | 'empty';

/**
 * Signal-based facade for ICD-10 coding suggestions (US_049 / SCR-017).
 *
 * Provided at the CodingSuggestionPanelComponent level so state resets on unmount.
 * - `loadingState`: drives @switch in the panel template.
 * - `lowConfidence`: triggers the amber banner (AC-3).
 * - `suggestions`: up to 3 ranked ICD-10 suggestions.
 */
@Injectable()
export class CodingSuggestionFacade {
  private readonly service = inject(CodingSuggestionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loadingState = signal<CodingLoadingState>('idle');
  readonly suggestions = signal<IcdSuggestionDto[]>([]);
  readonly lowConfidence = signal(false);

  loadSuggestions(patientId: string): void {
    this.loadingState.set('loading');

    this.service
      .getSuggestions(patientId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.suggestions.set(response.suggestions);
          this.lowConfidence.set(response.lowConfidence);

          if (response.insufficientEvidence && response.suggestions.length === 0) {
            this.loadingState.set('empty');
          } else {
            this.loadingState.set('loaded');
          }
        },
        error: () => {
          this.loadingState.set('error');
        },
      });
  }
}
