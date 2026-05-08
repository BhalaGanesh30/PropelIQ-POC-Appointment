import type { InsuranceVerificationRecord } from './insurance-verification-record.model';

/**
 * Server-side paginated result from GET /api/v1/insurance/verification-report
 * (EP-005 US_039 AC-1, Edge Case 1 — server-side pagination).
 */
export interface VerificationReportPagedResult {
  /** Current page of records (1-indexed). */
  records: InsuranceVerificationRecord[];
  /** Total number of records matching the active filter (all pages). */
  totalCount: number;
  /** Current 1-indexed page number. */
  page: number;
  /** Number of records per page. */
  pageSize: number;
}
