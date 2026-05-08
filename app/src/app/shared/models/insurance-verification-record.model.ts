/**
 * A single patient insurance verification record returned by
 * GET /api/v1/insurance/verification-report (EP-005 US_039 AC-1).
 */
export interface InsuranceVerificationRecord {
  /** UUID of the insurance profile record. */
  profileId: string;
  /** Display name of the patient. */
  patientName: string;
  /** Insurance provider name (decrypted). */
  providerName: string;
  /** Policy number (decrypted, may be partially masked in future). */
  policyNumber: string;
  /**
   * Soft-validation outcome stored on the record.
   * Values: SoftValidated | ValidationFailed | ValidationPending | Warning
   */
  validationStatus: InsuranceVerificationStatus;
  /** ISO-8601 timestamp when the record was last validated. */
  validatedAt: string;
}

/**
 * Subset of validation statuses surfaced in the report UI (AC-1, UXR-404).
 * 'Warning' is mapped to 'ValidationPending' in the display layer per AC-2.
 */
export type InsuranceVerificationStatus =
  | 'SoftValidated'
  | 'ValidationFailed'
  | 'ValidationPending'
  | 'Warning';
