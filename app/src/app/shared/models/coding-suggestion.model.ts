/**
 * A single clinical fact citation linked to an ICD-10 suggestion (AC-2).
 * Represents evidence extracted from the patient's clinical profile.
 */
export interface ClinicalFactCitationDto {
  factId: string;
  factType: string;
  name: string;
  value: string;
  factDate: string | null;
}

/**
 * A single AI-generated ICD-10 code suggestion (FR-MC-001).
 * Returned as part of the coding suggestions response.
 */
export interface IcdSuggestionDto {
  decisionId: string;
  icdCode: string;
  description: string;
  /** Confidence score 0.0–1.0. */
  confidence: number;
  rationale: string;
  citations: ClinicalFactCitationDto[];
}

/**
 * Response envelope for GET /api/v1/patients/{id}/coding-suggestions.
 * Contains up to 3 ranked ICD-10 suggestions with metadata flags.
 */
export interface CodingSuggestionResponseDto {
  suggestions: IcdSuggestionDto[];
  /** True when AI model confidence is below the configured threshold (AC-3). */
  lowConfidence: boolean;
  /** True when fewer than 3 codes could be generated (Edge Case 1). */
  insufficientEvidence: boolean;
}
