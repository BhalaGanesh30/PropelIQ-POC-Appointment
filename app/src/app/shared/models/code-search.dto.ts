/**
 * A single code search result returned from the code lookup API (SCR-018).
 */
export interface CodeResultDto {
  code: string;
  description: string;
  codeType: 'icd10' | 'cpt';
  /** True when the code is deprecated / inactive (ICD-10/CPT edition rollover). */
  isDeprecated: boolean;
  /** Whether the current clinician has favorited this code. */
  isFavorited: boolean;
}

/**
 * Response envelope for GET /api/v1/codes/search.
 */
export interface CodeSearchResponseDto {
  results: CodeResultDto[];
  totalCount: number;
}

/**
 * A single favorited code stored for the current clinician.
 */
export interface CodeFavoriteDto {
  code: string;
  description: string;
  codeType: 'icd10' | 'cpt';
}

/**
 * Request body for POST /api/v1/users/me/code-favorites.
 */
export interface AddFavoriteRequestDto {
  code: string;
  codeType: 'icd10' | 'cpt';
}
