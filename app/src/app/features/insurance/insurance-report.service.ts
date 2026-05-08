import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import type { VerificationReportPagedResult } from '../../shared/models/verification-report-paged-result.model';

/**
 * Service wrapping the insurance verification report API endpoints
 * (EP-005 US_039 AC-1, AC-3, AC-4, Edge Case 1).
 *
 * All HTTP calls are authenticated via the global JWT interceptor.
 *
 * Report endpoint:   GET /api/v1/insurance/verification-report
 * PDF export:        GET /api/v1/insurance/verification-report/export/pdf
 * CSV export:        GET /api/v1/insurance/verification-report/export/csv
 *
 * Exports include ALL filtered records regardless of the current page
 * (Edge Case 1 — the API streams the full dataset).
 */
@Injectable({ providedIn: 'root' })
export class InsuranceReportService {
  private readonly http = inject(HttpClient);

  private readonly baseUrl = '/api/v1/insurance/verification-report';

  /**
   * Fetches a page of insurance verification records.
   * Pass `null` or `undefined` for status to retrieve all statuses.
   *
   * @param status  Validation status filter, or null for all.
   * @param page    1-indexed page number.
   * @param pageSize  Number of records per page (default 25).
   */
  getReport(
    status: string | null | undefined,
    page: number,
    pageSize: number,
  ): Observable<VerificationReportPagedResult> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (status) {
      params = params.set('status', status);
    }

    return this.http.get<VerificationReportPagedResult>(this.baseUrl, { params });
  }

  /**
   * Exports all filtered records as a PDF blob (AC-3, Edge Case 1).
   * The caller triggers a browser file download from the returned blob.
   */
  exportPdf(status: string | null | undefined): Observable<Blob> {
    let params = new HttpParams();
    if (status) {
      params = params.set('status', status);
    }

    return this.http.get(`${this.baseUrl}/export/pdf`, {
      params,
      responseType: 'blob',
    });
  }

  /**
   * Exports all filtered records as a CSV blob (AC-4, Edge Case 1).
   * Columns: Patient Name, Insurance Provider, Policy Number,
   *          Validation Status, Validated Date — suitable for billing import.
   */
  exportCsv(status: string | null | undefined): Observable<Blob> {
    let params = new HttpParams();
    if (status) {
      params = params.set('status', status);
    }

    return this.http.get(`${this.baseUrl}/export/csv`, {
      params,
      responseType: 'blob',
    });
  }
}
