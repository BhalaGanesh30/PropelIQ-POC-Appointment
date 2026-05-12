/**
 * TypeScript interfaces for the SCR-022 Compliance Reports screen (US_058).
 *
 * Maps to the API contract exposed by ComplianceReportController
 * (POST /api/v1/admin/reports, GET, GET /{id}/download, GET /{id}/status).
 */

// ── Status / recurrence enums ─────────────────────────────────────────────────

export type ReportStatus =
  | 'Pending'
  | 'Generating'
  | 'Completed'
  | 'Failed';

export type RecurrencePattern =
  | 'Daily'
  | 'Weekly'
  | 'Monthly';

// ── Report models ─────────────────────────────────────────────────────────────

/** Single compliance report row returned by GET /api/v1/admin/reports. */
export interface ComplianceReport {
  id: string;
  reportType: string;
  periodStartUtc: string;
  periodEndUtc: string;
  status: ReportStatus;
  generatedAtUtc: string;
  totalAuditEvents: number;
  uniqueActors: number;
  anomalyCount: number;
  failedAccessAttempts: number;
  isAsync: boolean;
  jobId: string | null;
}

/** Paged wrapper returned by GET /api/v1/admin/reports. */
export interface ComplianceReportPagedResult {
  total: number;
  items: ComplianceReport[];
}

/** Request body sent to POST /api/v1/admin/reports. */
export interface GenerateReportRequest {
  reportType: string;
  periodStartUtc: string;
  periodEndUtc: string;
}

/**
 * Response from POST /api/v1/admin/reports.
 * - 200: sync; `id` is the new report id.
 * - 202: async; `jobId` is the tracking job id.
 */
export interface GenerateReportResponse {
  id?: string;
  jobId?: string;
  isAsync: boolean;
  status: string;
}

/** Response from GET /api/v1/admin/reports/{id}/status. */
export interface ReportJobStatus {
  id: string;
  status: string;
  reportId?: string;
}

// ── Schedule models ───────────────────────────────────────────────────────────

/** A compliance report schedule entry (AC-1). */
export interface ReportSchedule {
  id: string;
  name: string;
  reportType: string;
  recurrence: RecurrencePattern;
  isActive: boolean;
  lastRunAt: string | null;
  nextRunAt: string;
}

// ── Distribution models ───────────────────────────────────────────────────────

/** A single email recipient on a compliance distribution list (AC-3). */
export interface DistributionEntry {
  id: string;
  name: string;
  email: string;
  isActive: boolean;
}
