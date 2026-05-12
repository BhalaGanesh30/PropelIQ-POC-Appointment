/**
 * TypeScript interfaces for the Audit Log Viewer (SCR-021, US_056 task_003).
 */

/** A single audit record returned from GET /api/v1/admin/audit-logs. */
export interface AuditLogEntry {
  /** UUID of the audit record. */
  auditId: string;
  /** Event type string (e.g., "DataAccess", "ConfigChanged"). */
  eventType: string;
  /** UUID of the actor (authenticated user). */
  actorUserId: string;
  /** Resolved display name of the actor (may be null for system events). */
  actorName: string | null;
  /** Role of the actor at the time of the event. */
  actorRole: string | null;
  /** UUID of the affected entity. Null for entity-less events. */
  targetEntityId: string | null;
  /** Type name of the affected entity (e.g., "Patient", "Appointment"). */
  targetEntityType: string;
  /** UTC timestamp the event occurred. ISO 8601. */
  occurredAt: string;
  /** Structured metadata from the AuditDetails JSONB column. */
  metadata: Record<string, string> | null;
}

/** Query parameters for audit log filtering and pagination. */
export interface AuditLogQueryParams {
  actorUserId?: string;
  eventType?: string;
  from?: string;    // ISO 8601 UTC
  to?: string;      // ISO 8601 UTC
  entityId?: string;
  page: number;
  pageSize: number;
}

/** Response payload from GET /api/v1/admin/audit-logs. */
export interface AuditLogPagedResult {
  items: AuditLogEntry[];
}

/** Response from POST /api/v1/admin/audit-logs/export — contains the job ID. */
export interface ExportStartedResponse {
  jobId: string;
}

/** Canonical list of known event types for the filter dropdown. */
export const EVENT_TYPES: string[] = [
  'DataAccess',
  'Override',
  'ConfigChanged',
  'CodingReview',
  'LoginSuccess',
  'LoginFailure',
  'RoleAssigned',
  'AccountLockout',
  'PasswordReset',
  'PartitionArchived',
  'PartitionCreated',
  'BookingCreated',
  'BookingCancelled',
  'StaffBooking',
];

/** An active filter chip displayed in the Validation state. */
export interface ActiveFilter {
  key: keyof AuditLogQueryParams;
  label: string;
}
