import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  KpiExportFormat,
  KpiMetricType,
  KpiSummaryResponse,
  KpiTimeSeriesResponse,
} from './models/kpi.models';

/**
 * HTTP client for the KPI dashboard API (US_060).
 *
 * Base URL: /api/v1/admin/kpi
 * Auth: Bearer JWT via app-wide HTTP interceptor.
 * Backend enforces Admin role policy.
 */
@Injectable({ providedIn: 'root' })
export class KpiApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/kpi';

  /**
   * Returns four KPI card values for the given date range (AC-1, AC-2).
   * Edge case 1: response includes `isStale` flag when data is older than 1 hour.
   */
  getSummary(from: string, to: string): Observable<KpiSummaryResponse> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<KpiSummaryResponse>(`${this.base}/summary`, { params });
  }

  /**
   * Returns daily time-series data for a single KPI metric (AC-2).
   * Edge case 2: empty `points` array when no data exists for the period.
   */
  getTimeSeries(
    metric: KpiMetricType,
    from: string,
    to: string,
  ): Observable<KpiTimeSeriesResponse> {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<KpiTimeSeriesResponse>(`${this.base}/timeseries/${metric}`, { params });
  }

  /**
   * Generates a PDF or PNG export of the KPI summary and returns it as a Blob (AC-3).
   * AC-3 SLA: must complete within 3 seconds.
   */
  export(from: string, to: string, format: KpiExportFormat): Observable<Blob> {
    return this.http.post(
      `${this.base}/export`,
      { range: { from, to }, format },
      { responseType: 'blob' },
    );
  }
}
