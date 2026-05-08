import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { OverrideRequest } from '../../shared/models/override-request.model';
import { OverrideResponse } from '../../shared/models/override-response.model';
import { OverrideAuditEntry } from '../../shared/models/override-audit-entry.model';

/** Query parameters accepted by getOverrideAuditLog(). */
export interface OverrideAuditLogParams {
  /** ISO-8601 date string for the start of the date range filter (inclusive). */
  from?: string;
  /** ISO-8601 date string for the end of the date range filter (inclusive). */
  to?: string;
  /** Optional appointment UUID for single-appointment audit scope. */
  appointmentId?: string;
  /** Page size — defaults to 50. */
  pageSize?: number;
  /** 0-based page index. */
  page?: number;
}

/**
 * HTTP service for scheduling override operations (EP-004 US_034).
 *
 * FR-SO-004: Staff override of scheduling constraints with mandatory reason
 * capture and audit trail.
 *
 * submitOverride()      — POST /api/v1/scheduling/override (AC-2)
 * getOverrideAuditLog() — GET  /api/v1/audit?actionType=Override (AC-4)
 */
@Injectable({ providedIn: 'root' })
export class OverrideService {
  private readonly http = inject(HttpClient);
  private readonly overrideUrl = `${environment.apiBaseUrl}/scheduling/override`;
  private readonly auditUrl    = `${environment.apiBaseUrl}/audit`;

  /**
   * Submits the override request to the server.
   *
   * AC-2: Sends reason + constraint metadata; server creates the override
   * record and writes an immutable AuditRecord, then returns overrideId and
   * auditRecordId so the caller can correlate the result.
   */
  submitOverride(payload: OverrideRequest): Observable<OverrideResponse> {
    return this.http.post<OverrideResponse>(this.overrideUrl, payload);
  }

  /**
   * Fetches override audit events filtered by actionType=Override.
   *
   * AC-4: Returns entries with actor name/role, overridden constraint, and
   * full reason text for display in the admin audit log table.
   */
  getOverrideAuditLog(params: OverrideAuditLogParams = {}): Observable<OverrideAuditEntry[]> {
    let httpParams = new HttpParams().set('actionType', 'Override');

    if (params.from)           { httpParams = httpParams.set('from',          params.from); }
    if (params.to)             { httpParams = httpParams.set('to',            params.to); }
    if (params.appointmentId)  { httpParams = httpParams.set('appointmentId', params.appointmentId); }
    if (params.pageSize != null) { httpParams = httpParams.set('pageSize',    params.pageSize.toString()); }
    if (params.page != null)     { httpParams = httpParams.set('page',        params.page.toString()); }

    return this.http.get<OverrideAuditEntry[]>(this.auditUrl, { params: httpParams });
  }
}
