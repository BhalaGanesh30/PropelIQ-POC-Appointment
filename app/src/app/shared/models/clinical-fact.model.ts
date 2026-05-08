/**
 * A single clinical fact extracted from a document by the AI pipeline.
 * Maps from the BE ClinicalFactDto returned by GET /api/v1/patients/{id}/profile.
 */
export interface ClinicalFactDto {
  /** Backend GUID for the fact. */
  factId: string;
  /** Patient GUID. */
  patientId: string;
  /** Source document GUID. */
  documentId: string;
  /** Category: medication | allergy | diagnosis | finding */
  factType: 'medication' | 'allergy' | 'diagnosis' | 'finding' | string;
  /** Canonical entity name (e.g. "Acetaminophen", "Penicillin allergy"). */
  name: string | null;
  /** Full structured value (e.g. "500mg twice daily"). */
  value: string;
  /** Confidence score 0.0–1.0. */
  confidenceScore: number;
  /** True when confidence < threshold — surfaced as review indicator. */
  needsReview: boolean;
  /** Verbatim text segment this fact was extracted from (AIR-004). */
  sourceText: string | null;
  /** True once a clinician has verified this fact. */
  verified: boolean;
  /** ISO-8601 clinical date (nullable). */
  factDate: string | null;
  /** Display name of the source document. */
  documentDisplayName: string | null;
  /** ISO-8601 upload timestamp of the source document. */
  documentUploadedAt: string | null;
  /** ETag from last server response — used for If-Match optimistic concurrency (Edge Case 1). */
  etag?: string | null;
  /** True when this fact is linked to a coding decision — surfaces warning on edit (Edge Case 2). */
  referencedByCodingDecision?: boolean;
}

/** Request body for PATCH /api/v1/clinical-facts/{id}. */
export interface PatchFactRequestDto {
  name: string;
  value: string;
}

/** Response body from PATCH /api/v1/clinical-facts/{id}. */
export interface PatchFactResponseDto extends ClinicalFactDto {
  referencedByCodingDecision: boolean;
}

/** Body returned on HTTP 409 conflict. */
export interface ConcurrencyConflictResponseDto {
  currentValue: string;
  currentName: string | null;
}

/** A single audit history entry for GET /api/v1/clinical-facts/{id}/history. */
export interface FactHistoryEntryDto {
  auditId: string;
  previousName: string | null;
  previousValue: string;
  editorDisplayName: string;
  timestamp: string;  // ISO-8601
}

/** Partial source descriptor — returned when one data source fails. */
export interface PartialSourceDto {
  /** Human-readable name of the unavailable data source. */
  sourceName: string;
  /** Short error reason. */
  reason: string;
}

/** Response envelope for GET /api/v1/patients/{id}/profile. */
export interface PatientProfileDto {
  facts: ClinicalFactDto[];
  /** Non-empty when some sources returned errors (Edge Case 1). */
  partialSources: PartialSourceDto[];
}

/** Thin patient header model for the profile page. */
export interface PatientHeaderDto {
  id: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  mrn: string;
}
