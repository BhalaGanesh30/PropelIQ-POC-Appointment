/**
 * Severity levels for drug-drug and drug-allergy conflicts (AC-1, UXR-404).
 * UXR-404 color mapping: critical=red, high=orange, moderate=yellow, low=blue.
 */
export type ConflictSeverity = 'critical' | 'high' | 'moderate' | 'low';

/** Conflict type discriminator. */
export type ConflictType = 'drug-drug' | 'drug-allergy';

/**
 * A single detected drug-drug or drug-allergy conflict (FR-CA-003).
 * Pairs are deduplicated on the backend — highest severity per pair only (Edge Case 2).
 */
export interface ConflictAlertDto {
  /** Unique conflict identifier (used for acknowledgment). */
  conflictId: string;
  /** Whether this is a drug-drug or drug-allergy conflict. */
  conflictType: ConflictType;
  /** Severity classification (AC-1). */
  severity: ConflictSeverity;
  /** Human-readable description of the conflict. */
  description: string;
  /** First drug (or allergen) in the conflicting pair. */
  drugA: string;
  /** Second drug in the pair. Null for drug-allergy conflicts where drugB is the allergen. */
  drugB: string | null;
  /** True once a clinician has acknowledged this conflict (AC-4). */
  acknowledged: boolean;
  /** ISO-8601 timestamp when this conflict was acknowledged. Null if not yet acknowledged. */
  acknowledgedAt: string | null;
  /** Display name of the clinician who acknowledged. Null if not yet acknowledged. */
  acknowledgedBy: string | null;
}

/**
 * Full response envelope for GET /api/v1/patients/{id}/conflicts.
 * Edge Case 1: rulesStale signals outdated detection rules.
 */
export interface ConflictAlertsResponseDto {
  /** All detected conflicts for the patient, deduplicated. */
  alerts: ConflictAlertDto[];
  /**
   * True when the conflict detection rules database is stale.
   * Display a non-blocking amber warning (Edge Case 1).
   */
  rulesStale: boolean;
}
