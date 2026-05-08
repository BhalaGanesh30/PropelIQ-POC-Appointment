import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import type {
  ClinicalFactDto,
  PatchFactRequestDto,
  PatchFactResponseDto,
  FactHistoryEntryDto,
} from '../../shared/models/clinical-fact.model';

/**
 * HTTP service for individual clinical-fact operations (US_047).
 *
 * Endpoints:
 *   PATCH  /api/v1/clinical-facts/{id}         — edit name + value (If-Match ETag)
 *   POST   /api/v1/clinical-facts/{id}/verify   — one-click verification
 *   GET    /api/v1/clinical-facts/{id}/history  — audit history entries
 */
@Injectable({ providedIn: 'root' })
export class ClinicalFactService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/clinical-facts';

  /**
   * Patches a fact's `name` and `value`.
   * Sends `If-Match: {etag}` for optimistic concurrency (Edge Case 1).
   * Errors on HTTP 409 with the original `HttpErrorResponse` so callers
   * can extract `error.currentValue` / `error.currentName`.
   */
  patchFact(
    factId: string,
    dto: PatchFactRequestDto,
    etag: string | null,
  ): Observable<PatchFactResponseDto> {
    const headers = etag
      ? new HttpHeaders({ 'If-Match': etag })
      : new HttpHeaders();

    return this.http.patch<PatchFactResponseDto>(
      `${this.base}/${factId}`,
      dto,
      { headers, observe: 'response' },
    ).pipe(
      map((response) => {
        const body = response.body!;
        const responseEtag =
          response.headers.get('ETag') ?? response.headers.get('Etag');
        // Carry the updated ETag forward for future concurrency checks.
        return { ...body, etag: responseEtag };
      }),
      catchError((err: HttpErrorResponse) => throwError(() => err)),
    );
  }

  /**
   * Verifies a fact without editing it (one-click approval — AC-2).
   * Returns the updated fact with `verified: true`.
   */
  verifyFact(factId: string): Observable<ClinicalFactDto> {
    return this.http.post<ClinicalFactDto>(`${this.base}/${factId}/verify`, {});
  }

  /**
   * Fetches the chronological audit history for a fact (AC-3).
   * Lazily called on first expansion-panel open.
   */
  getFactHistory(factId: string): Observable<FactHistoryEntryDto[]> {
    return this.http.get<FactHistoryEntryDto[]>(`${this.base}/${factId}/history`);
  }
}
