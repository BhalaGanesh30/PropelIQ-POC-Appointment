import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import type { CptSuggestionResponseDto } from '../../shared/models/cpt-suggestion.model';

/** Sentinel returned when the appointment type is not CPT-mappable (Edge Case 1). */
const EMPTY_RESPONSE: CptSuggestionResponseDto = {
  cptSuggestions: [],
  emSuggestion: null,
  lowConfidence: false,
  staleDatabaseWarning: false,
  noSuggestionForAppointmentType: true,
};

/**
 * HTTP service for CPT / E/M coding suggestion retrieval (US_050).
 *
 * Endpoint:
 *   GET /api/v1/patients/{patientId}/coding-suggestions/cpt?appointmentId={appointmentId}
 *
 * Maps HTTP 422 to the no-suggestion sentinel (Edge Case 1: appointment type not mappable).
 * All other HTTP errors are re-thrown for the facade to handle as an error state.
 */
@Injectable({ providedIn: 'root' })
export class CptSuggestionService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/patients';

  getCptSuggestions(
    patientId: string,
    appointmentId: string,
  ): Observable<CptSuggestionResponseDto> {
    return this.http
      .get<CptSuggestionResponseDto>(
        `${this.base}/${patientId}/coding-suggestions/cpt`,
        { params: { appointmentId } },
      )
      .pipe(
        map((response) =>
          response.noSuggestionForAppointmentType
            ? { ...response, ...EMPTY_RESPONSE }
            : response,
        ),
        catchError((err: HttpErrorResponse) => {
          if (err.status === 422) {
            return of(EMPTY_RESPONSE);
          }
          throw err;
        }),
      );
  }
}
