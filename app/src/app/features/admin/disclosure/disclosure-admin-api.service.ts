import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DisclosureReviewPagedResult,
  DisclosureReport,
  ReviewDisclosureAction,
  DisclosureRequest,
} from '../../settings/disclosure/models/disclosure.models';

/**
 * HTTP client for the staff/admin disclosure review API (US_057, AC-5).
 *
 * Base URL: /api/v1/admin/disclosure-requests
 * Auth: Bearer JWT via app-wide HTTP interceptor.
 * Backend enforces StaffOrAdmin policy.
 */
@Injectable({ providedIn: 'root' })
export class DisclosureAdminApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/disclosure-requests';

  /**
   * Lists disclosure requests optionally filtered by status.
   * Defaults to PendingReview if no status supplied (queue view).
   */
  list(
    status: string | null = 'PendingReview',
    page = 1,
    pageSize = 20,
  ): Observable<DisclosureReviewPagedResult> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (status) {
      params = params.set('status', status);
    }

    return this.http.get<DisclosureReviewPagedResult>(this.base, { params });
  }

  /** Returns the compiled report for staff preview. */
  getReport(requestId: string): Observable<DisclosureReport> {
    return this.http.get<DisclosureReport>(`${this.base}/${requestId}/report`);
  }

  /** Approves or rejects a disclosure request (AC-5). */
  review(requestId: string, action: ReviewDisclosureAction): Observable<DisclosureRequest> {
    return this.http.put<DisclosureRequest>(
      `${this.base}/${requestId}/review`,
      action,
    );
  }
}
