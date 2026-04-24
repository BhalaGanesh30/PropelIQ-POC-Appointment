import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AppointmentHistoryFilter,
  AppointmentHistoryResponse,
} from './models/appointment-history.models';

/**
 * HTTP client for the appointment history and PDF export endpoints.
 *
 * GET /api/v1/appointmenthistory        — paginated filtered history (AC-1..3).
 * GET /api/v1/appointmenthistory/export — PDF blob for all filtered records (AC-4).
 *
 * Bearer token is attached automatically by the app-wide HTTP auth interceptor.
 */
@Injectable({ providedIn: 'root' })
export class AppointmentHistoryApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/appointmenthistory';

  /**
   * Fetches a paginated, date-descending appointment history with optional
   * status and date-range filters (AC-1, AC-2, AC-3).
   * Edge case: empty history returns items=[] and totalCount=0.
   */
  getHistory(
    filter: AppointmentHistoryFilter,
  ): Observable<AppointmentHistoryResponse> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.status) params = params.set('status', filter.status);
    if (filter.dateFrom) params = params.set('dateFrom', filter.dateFrom);
    if (filter.dateTo) params = params.set('dateTo', filter.dateTo);

    return this.http.get<AppointmentHistoryResponse>(this.baseUrl, { params });
  }

  /**
   * Downloads all filtered appointments as a PDF blob (AC-4).
   * Pagination is ignored by the server — the PDF contains the complete result set.
   */
  exportPdf(filter: AppointmentHistoryFilter): Observable<Blob> {
    let params = new HttpParams();

    if (filter.status) params = params.set('status', filter.status);
    if (filter.dateFrom) params = params.set('dateFrom', filter.dateFrom);
    if (filter.dateTo) params = params.set('dateTo', filter.dateTo);

    return this.http.get(`${this.baseUrl}/export`, {
      params,
      responseType: 'blob',
    });
  }
}
