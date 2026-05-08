import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import type { TimelineQueryParams, TimelineResponseDto } from '../../shared/models/timeline-event.model';

/**
 * HTTP service for the clinical timeline API (US_048).
 *
 * Endpoint:
 *   GET /api/v1/patients/{id}/timeline?category=&dateFrom=&dateTo=
 */
@Injectable({ providedIn: 'root' })
export class ClinicalTimelineService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/patients';

  /**
   * Fetches timeline events for a patient, optionally filtered by category and date range.
   * @param patientId  Patient UUID.
   * @param params     Optional category and date range filters (AC-2, AC-3).
   */
  getTimeline(
    patientId: string,
    params: TimelineQueryParams = {},
  ): Observable<TimelineResponseDto> {
    let httpParams = new HttpParams();
    if (params.category) httpParams = httpParams.set('category', params.category);
    if (params.dateFrom)  httpParams = httpParams.set('dateFrom', params.dateFrom);
    if (params.dateTo)    httpParams = httpParams.set('dateTo',   params.dateTo);

    return this.http.get<TimelineResponseDto>(
      `${this.base}/${patientId}/timeline`,
      { params: httpParams },
    );
  }
}
