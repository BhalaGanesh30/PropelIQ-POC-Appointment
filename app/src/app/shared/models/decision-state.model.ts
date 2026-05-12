/**
 * Workflow states for a single coding suggestion decision card (US_051 / SCR-017).
 *
 * - pending:  No action taken yet — Accept / Modify / Reject buttons visible (UXR-108).
 * - accepted: Clinician accepted the AI suggestion — green border, "Accepted" chip (AC-1).
 * - modified: Clinician saved a modified code — "Modified from AI" chip; same visual as accepted (AC-2).
 * - rejected: Clinician rejected the suggestion — gray + strikethrough; "Search Code" link (AC-3).
 * - editing:  Inline edit form is open — transient, never sent to API.
 */
export type DecisionState = 'pending' | 'accepted' | 'modified' | 'rejected' | 'editing';

/**
 * Runtime entry in the `CodingDecisionFacade` signal map.
 * Persists `finalCode` / `finalDescription` so accepted/modified cards can display the resolved value
 * and the pending-submission block banner can list pending item codes.
 */
export interface DecisionEntry {
  state: DecisionState;
  /** Finalised code value (original for accepted; edited value for modified). */
  finalCode: string;
  finalDescription: string;
}

/**
 * API response DTO returned by accept / modify / reject endpoints (US_051).
 */
export interface DecisionStateDto {
  decisionId: string;
  action: 'accepted' | 'modified' | 'rejected';
  finalCode: string;
  finalDescription: string;
}

/** Request body for POST /api/v1/coding-decisions/{id}/accept (AC-1). */
export interface AcceptRequestDto {
  decisionId: string;
}

/** Request body for PATCH /api/v1/coding-decisions/{id}/modify (AC-2). */
export interface ModifyRequestDto {
  decisionId: string;
  finalCode: string;
  finalDescription: string;
}

/** Request body for POST /api/v1/coding-decisions/{id}/reject (AC-3). */
export interface RejectRequestDto {
  decisionId: string;
}
