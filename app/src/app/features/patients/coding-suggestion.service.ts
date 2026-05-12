import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import type { CodingSuggestionResponseDto } from '../../shared/models/coding-suggestion.model';

/** Sentinel value returned when the API responds with HTTP 422 (Edge Case 2). */
const EMPTY_RESPONSE: CodingSuggestionResponseDto = {
  suggestions: [],
  lowConfidence: false,
  insufficientEvidence: true,
};

/**
 * HTTP service for coding suggestion retrieval (US_049).
 *
 * Endpoint:
 *   GET /api/v1/patients/{id}/coding-suggestions
 *
 * Maps HTTP 422 to an empty-state response (Edge Case 2: no extracted clinical facts).
 * All other HTTP errors are re-thrown for the facade to handle.
 */
@Injectable({ providedIn: 'root' })
export class CodingSuggestionService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/patients';

  getSuggestions(patientId: string): Observable<CodingSuggestionResponseDto> {
    return this.http
      .get<CodingSuggestionResponseDto>(
        `${this.base}/${patientId}/coding-suggestions`,
      )
      .pipe(
        catchError((err: HttpErrorResponse) => {
          if (err.status === 422) {
            return of(EMPTY_RESPONSE);
          }
          throw err;
        }),
      );
  }
}
