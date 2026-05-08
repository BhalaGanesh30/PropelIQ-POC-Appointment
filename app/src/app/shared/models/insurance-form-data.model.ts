/**
 * Insurance entry submitted to POST /api/v1/insurance/validate and
 * POST /api/v1/insurance (FR-IP-001).
 *
 * tier distinguishes primary from secondary insurance — both use the same
 * DTO shape so a single validate() call covers each tier independently.
 */
export type InsuranceTier = 'Primary' | 'Secondary';

export interface InsuranceFormData {
  /** Tier discriminator. */
  tier: InsuranceTier;
  /** Policy number — alphanumeric 5–30 chars (AC-1 format check). */
  policyNumber: string;
  /** Insurance provider identifier code (e.g. "ACME-HEALTH"). */
  providerCode: string;
  /** Human-readable provider name (e.g. "Acme Health Plan"). */
  providerName: string;
  /** Optional group / plan number. */
  groupNumber?: string;
  /** Front-of-card image file (JPEG/PNG ≤ 5 MB, UXR-505). */
  cardImageFront?: File;
  /** Back-of-card image file (JPEG/PNG ≤ 5 MB, UXR-505). */
  cardImageBack?: File;
}

/** Shape of the saved InsuranceProfile returned by POST /api/v1/insurance. */
export interface InsuranceProfile {
  id: string;
  patientId: string;
  tier: InsuranceTier;
  policyNumber: string;
  providerCode: string;
  providerName: string;
  groupNumber?: string;
  validationStatus: string;
  createdAt: string;
}
