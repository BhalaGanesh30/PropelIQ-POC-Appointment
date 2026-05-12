// ── Status enum ───────────────────────────────────────────────────────────────

export type DisclosureStatus =
  | 'Submitted'
  | 'Compiling'
  | 'PendingReview'
  | 'Approved'
  | 'Delivered'
  | 'Rejected';

// ── Disclosure request ────────────────────────────────────────────────────────

/** Read model returned from GET /api/v1/patients/me/disclosure-requests */
export interface DisclosureRequest {
  id: string;
  patientId: string;
  fromDateUtc: string;   // ISO-8601 UTC
  toDateUtc: string;     // ISO-8601 UTC
  status: DisclosureStatus;
  requestedAt: string;   // ISO-8601 UTC (BaseEntity.CreatedAt)
  compiledAt: string | null;
  reviewedBy: string | null;
  reviewedAt: string | null;
  reviewNotes: string | null;
  deliveredAt: string | null;
  deliveryMethod: string | null;
  reportId: string | null;
}

/** POST /api/v1/patients/me/disclosure-requests body */
export interface SubmitDisclosureRequest {
  fromDateUtc: string;  // ISO-8601 UTC
  toDateUtc: string;    // ISO-8601 UTC
}

/** Response from submission (201 Created) */
export interface SubmitDisclosureResponse {
  id: string;
  status: DisclosureStatus;
}

// ── Staff review ──────────────────────────────────────────────────────────────

/** PUT /api/v1/admin/disclosure-requests/{id}/review body */
export interface ReviewDisclosureAction {
  approved: boolean;
  notes: string | null;
}

/** Compiled disclosure report (staff preview) */
export interface DisclosureReport {
  id: string;
  disclosureRequestId: string;
  accessEventCount: number;
  generatedAt: string;     // ISO-8601 UTC
  reportJson: string;      // Raw JSON string to display for staff preview
  hasDownloadLink: boolean;
}

/** Paginated list response from GET /api/v1/admin/disclosure-requests */
export interface DisclosureReviewPagedResult {
  items: DisclosureRequest[];
  total: number;
}

// ── Access log ────────────────────────────────────────────────────────────────

/** Single access log entry from GET /api/v1/admin/access-logs */
export interface AccessLogEntry {
  auditId: string;
  actorUserId: string;
  actorName: string | null;
  actorRole: string;
  resourceType: string;
  entityId: string | null;
  occurredAt: string;    // ISO-8601 UTC
}

/** Paginated response from access log query */
export interface AccessLogPagedResult {
  total: number;
  items: AccessLogEntry[];
}

/** Query parameters for GET /api/v1/admin/access-logs */
export interface AccessLogQueryParams {
  patientId: string;
  fromUtc?: string;
  toUtc?: string;
  page: number;
  pageSize: number;
}
