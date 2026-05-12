import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ComplianceReport,
  ComplianceReportPagedResult,
  DistributionEntry,
  GenerateReportRequest,
  GenerateReportResponse,
  ReportJobStatus,
  ReportSchedule,
} from './models/compliance.models';

/**
 * HTTP client for the compliance report API (US_058).
 *
 * Base URL: /api/v1/admin/reports
 * Auth: Bearer JWT via app-wide HTTP interceptor.
 * Backend enforces Admin role policy.
 */
@Injectable({ providedIn: 'root' })
export class ComplianceApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/reports';

  // ── Reports ───────────────────────────────────────────────────────────────

  /** Triggers on-demand report generation (AC-4). Returns 200 (sync) or 202 (async). */
  generate(req: GenerateReportRequest): Observable<GenerateReportResponse> {
    return this.http.post<GenerateReportResponse>(this.base, req);
  }

  /** Lists generated reports, newest first, paged (AC-2). */
  list(page: number, pageSize: number): Observable<ComplianceReportPagedResult> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http.get<ComplianceReportPagedResult>(this.base, { params });
  }

  /** Returns a single report by ID. */
  getReport(id: string): Observable<ComplianceReport> {
    return this.http.get<ComplianceReport>(`${this.base}/${id}`);
  }

  /** Downloads the PDF content as a Blob (AC-2). */
  downloadPdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/download`, { responseType: 'blob' });
  }

  /** Polls the async job status (edge case 1). */
  getJobStatus(jobId: string): Observable<ReportJobStatus> {
    return this.http.get<ReportJobStatus>(`${this.base}/${jobId}/status`);
  }

  // ── Schedules ─────────────────────────────────────────────────────────────

  /** Lists all configured report schedules (AC-1). */
  listSchedules(): Observable<ReportSchedule[]> {
    return this.http.get<ReportSchedule[]>(`${this.base}/schedules`);
  }

  /** Toggles the active flag on a schedule (AC-1). */
  toggleSchedule(id: string, isActive: boolean): Observable<ReportSchedule> {
    return this.http.patch<ReportSchedule>(`${this.base}/schedules/${id}`, { isActive });
  }

  // ── Distribution list ─────────────────────────────────────────────────────

  /** Lists all email recipients in the distribution list (AC-3). */
  listRecipients(): Observable<DistributionEntry[]> {
    return this.http.get<DistributionEntry[]>(`${this.base}/distribution`);
  }

  /** Adds a new recipient to the distribution list (AC-3). */
  addRecipient(entry: Pick<DistributionEntry, 'name' | 'email'>): Observable<DistributionEntry> {
    return this.http.post<DistributionEntry>(`${this.base}/distribution`, entry);
  }

  /** Removes a recipient from the distribution list (AC-3). */
  removeRecipient(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/distribution/${id}`);
  }

  /** Toggles active/inactive on a recipient (AC-3). */
  toggleRecipient(id: string, isActive: boolean): Observable<DistributionEntry> {
    return this.http.patch<DistributionEntry>(`${this.base}/distribution/${id}`, { isActive });
  }
}
