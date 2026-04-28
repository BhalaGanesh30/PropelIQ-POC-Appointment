import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppointmentRiskScore } from './models/risk-score.models';

/**
 * HTTP client for the no-show risk scores API (US_028 task_002).
 *
 * GET /api/v1/appointments/risk-scores — returns cached or freshly-scored
 * risk data for all non-cancelled appointments in a date window.
 *
 * Authentication is attached by the HTTP auth interceptor — no manual header
 * manipulation is required.
 * Staff/Admin role is enforced server-side; the frontend must only call this
 * from staff-facing pages.
 */
@Injectable({ providedIn: 'root' })
export class RiskScoreApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/appointments/risk-scores';

  /**
   * Fetch risk scores for appointments in the [from, to] date range.
   * @param from ISO-8601 UTC string (e.g. "2026-04-28T00:00:00Z")
   * @param to   ISO-8601 UTC string (e.g. "2026-04-29T00:00:00Z")
   */
  getRiskScores(from: string, to: string): Observable<AppointmentRiskScore[]> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<AppointmentRiskScore[]>(this.baseUrl, { params });
  }
}
