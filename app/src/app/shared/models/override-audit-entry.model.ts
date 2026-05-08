/**
 * A single override event returned by GET /api/v1/audit?actionType=Override
 * (EP-004 US_034 AC-4).
 *
 * Rendered in the OverrideAuditLogComponent table.
 */
export interface OverrideAuditEntry {
  /** UUID of the underlying AuditRecord. */
  auditId: string;

  /** Display name of the staff member who performed the override. */
  actorName: string;

  /** Role of the actor (Staff | Admin). */
  actorRole: string;

  /** Human-readable description of the scheduling constraint that was overridden. */
  constraint: string;

  /** Staff-provided justification text (max 500 characters). */
  reason: string;

  /** ISO-8601 UTC timestamp when the override was applied. */
  timestamp: string;

  /** UUID of the affected appointment (optional — for deep-linking). */
  appointmentId?: string;
}
