import {
  DestroyRef,
  Injectable,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { CptSuggestionService } from './cpt-suggestion.service';
import type { CptSuggestionDto, EmSuggestionDto } from '../../shared/models/cpt-suggestion.model';

export type CptLoadingState = 'idle' | 'loading' | 'loaded' | 'empty' | 'error';

/**
 * Signal-based facade for CPT / E/M coding suggestions (US_050 / SCR-017).
 *
 * Provided at the CodingSuggestionPanelComponent level alongside CodingSuggestionFacade,
 * so CPT state is scoped to the panel lifecycle and resets on unmount.
 *
 * - `cptLoadingState`: drives @switch in the CPT section template.
 * - `cptLowConfidence`: triggers "Manual coding recommended" banner (AC-4).
 * - `staleDatabaseWarning`: triggers StaleCptDatabaseBannerComponent (Edge Case 2).
 * - `cptSuggestions`: up to N ranked CPT suggestions.
 * - `emSuggestion`: single E/M level suggestion (may be null).
 */
@Injectable()
export class CptSuggestionFacade {
  private readonly service = inject(CptSuggestionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly cptLoadingState = signal<CptLoadingState>('idle');
  readonly cptSuggestions = signal<CptSuggestionDto[]>([]);
  readonly emSuggestion = signal<EmSuggestionDto | null>(null);
  readonly cptLowConfidence = signal(false);
  readonly staleDatabaseWarning = signal(false);

  loadCptSuggestions(patientId: string, appointmentId: string): void {
    this.cptLoadingState.set('loading');

    this.service
      .getCptSuggestions(patientId, appointmentId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.cptSuggestions.set(response.cptSuggestions);
          this.emSuggestion.set(response.emSuggestion);
          this.cptLowConfidence.set(response.lowConfidence);
          this.staleDatabaseWarning.set(response.staleDatabaseWarning);

          if (response.noSuggestionForAppointmentType) {
            this.cptLoadingState.set('empty');
          } else {
            this.cptLoadingState.set('loaded');
          }
        },
        error: () => {
          this.cptLoadingState.set('error');
        },
      });
  }
}
