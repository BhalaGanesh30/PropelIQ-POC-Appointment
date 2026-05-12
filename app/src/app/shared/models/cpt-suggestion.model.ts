import type { ClinicalFactCitationDto } from './coding-suggestion.model';

/**
 * A single AI-generated CPT procedure code suggestion (US_050 / FR-MC-002).
 */
export interface CptSuggestionDto {
  decisionId: string;
  cptCode: string;
  description: string;
  /** Confidence score 0.0–1.0. */
  confidence: number;
  rationale: string;
  citations: ClinicalFactCitationDto[];
}

/**
 * AI-generated E/M (Evaluation & Management) level suggestion (US_050 / AC-3).
 * complexityFactors lists the clinical factors driving the E/M level selection.
 */
export interface EmSuggestionDto {
  decisionId: string;
  emLevel: string;
  description: string;
  /** Confidence score 0.0–1.0. */
  confidence: number;
  rationale: string;
  /** Clinical complexity factors contributing to this E/M level (AC-3). */
  complexityFactors: string[];
}

/**
 * Response envelope for GET /api/v1/patients/{id}/coding-suggestions/cpt.
 */
export interface CptSuggestionResponseDto {
  cptSuggestions: CptSuggestionDto[];
  emSuggestion: EmSuggestionDto | null;
  lowConfidence: boolean;
  /** True when the CPT database is stale (>90 days) — Edge Case 2. */
  staleDatabaseWarning: boolean;
  /** True when the appointment type cannot be mapped to CPT codes — Edge Case 1. */
  noSuggestionForAppointmentType: boolean;
}
