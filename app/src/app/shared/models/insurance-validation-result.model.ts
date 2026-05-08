/**
 * Validation status assigned to an insurance record after soft-validation (FR-IP-001).
 *
 * SoftValidated  — AC-3: policy format and provider code passed all checks.
 * Warning        — AC-2: format mismatch detected; booking NOT blocked.
 * ValidationFailed — AC-4: completely failed; flagged for staff review.
 * ValidationPending — Edge Case 1: reference DB unavailable; retry queued.
 */
export type InsuranceValidationStatus =
  | 'SoftValidated'
  | 'Warning'
  | 'ValidationFailed'
  | 'ValidationPending';

/** Per-field warning detail returned by the validate endpoint. */
export interface InsuranceValidationWarning {
  /** Affected field name (e.g. "policyNumber", "providerCode"). */
  field: string;
  /** Human-readable message shown inline next to the field. */
  message: string;
}

/**
 * Response from POST /api/v1/insurance/validate (AC-1).
 *
 * Regardless of status the FE always allows the booking flow to continue (AC-2).
 */
export interface InsuranceValidationResult {
  /** Overall soft-validation status. */
  status: InsuranceValidationStatus;
  /** Zero or more non-blocking warnings — shown as amber banners (UXR-404). */
  warnings: InsuranceValidationWarning[];
  /** True when the provider code matched the reference database. */
  providerMatch: boolean;
  /** True when the policy number passes the expected format for the provider. */
  policyFormatValid: boolean;
  /** Human-readable message summarising the result (shown in the status banner). */
  message?: string;
}
