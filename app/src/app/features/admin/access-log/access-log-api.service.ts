import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AccessLogPagedResult,
  AccessLogQueryParams,
} from '../../settings/disclosure/models/disclosure.models';

/**
 * HTTP client for the admin access log API (US_057, AC-4).
 *
 * Base URL: /api/v1/admin/access-logs
 * Auth: Bearer JWT via app-wide HTTP interceptor.
 * Backend enforces StaffOrAdmin policy.
 *
 * GET /api/v1/admin/access-logs?patientId=&fromUtc=&toUtc=&page=&pageSize=
 * Returns AccessLogPagedResult ordered chronologically ascending.
 */
@Injectable({ providedIn: 'root' })
export class AccessLogApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/access-logs';

  query(params: AccessLogQueryParams): Observable<AccessLogPagedResult> {
    let httpParams = new HttpParams()
      .set('patientId', params.patientId)
      .set('page', params.page)
      .set('pageSize', params.pageSize);

    if (params.fromUtc) {
      httpParams = httpParams.set('fromUtc', params.fromUtc);
    }
    if (params.toUtc) {
      httpParams = httpParams.set('toUtc', params.toUtc);
    }

    return this.http.get<AccessLogPagedResult>(this.base, { params: httpParams });
  }
}
