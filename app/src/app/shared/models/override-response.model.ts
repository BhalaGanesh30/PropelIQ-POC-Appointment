/**
 * Response body from POST /api/v1/scheduling/override (EP-004 US_034 AC-2).
 */
export interface OverrideResponse {
  /** UUID of the created override record. */
  overrideId: string;

  /** UUID of the immutable AuditRecord written on the server (AC-2, NFR-010). */
  auditRecordId: string;

  /** Human-readable outcome (e.g., "Applied"). */
  status: string;
}
