import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AuditLogEntry,
  AuditLogQueryParams,
  ExportStartedResponse,
} from './models/audit-log.models';

/**
 * HTTP client service for the admin audit log API (US_056 task_003, AC-4).
 *
 * Endpoints:
 * - GET  /api/v1/admin/audit-logs              — filtered + paginated query
 * - POST /api/v1/admin/audit-logs/export       — trigger async CSV export
 * - GET  /api/v1/admin/audit-logs/export/{id}  — poll / download result
 */
@Injectable({ providedIn: 'root' })
export class AuditLogApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/admin/audit-logs';

  /**
   * Queries the audit log with optional filters and pagination.
   * Maps client-side param names to the server's query string parameters.
   */
  query(params: AuditLogQueryParams): Observable<AuditLogEntry[]> {
    let httpParams = new HttpParams()
      .set('page', params.page)
      .set('pageSize', params.pageSize);

    if (params.actorUserId) httpParams = httpParams.set('actorUserId', params.actorUserId);
    if (params.eventType)   httpParams = httpParams.set('eventType',   params.eventType);
    if (params.from)        httpParams = httpParams.set('from',        params.from);
    if (params.to)          httpParams = httpParams.set('to',          params.to);
    if (params.entityId)    httpParams = httpParams.set('entityId',    params.entityId);

    return this.http.get<AuditLogEntry[]>(this.baseUrl, { params: httpParams });
  }

  /**
   * Triggers asynchronous CSV export for the current filter set.
   * Server returns 202 Accepted with a `jobId` for polling.
   */
  triggerExport(params: AuditLogQueryParams): Observable<ExportStartedResponse> {
    return this.http.post<ExportStartedResponse>(`${this.baseUrl}/export`, params);
  }

  /**
   * Polls or downloads an export job by ID.
   * - 202: job still processing.
   * - 200: CSV file streamed — caller opens the URL directly.
   */
  getExportDownloadUrl(jobId: string): string {
    return `${this.baseUrl}/export/${jobId}`;
  }
}
