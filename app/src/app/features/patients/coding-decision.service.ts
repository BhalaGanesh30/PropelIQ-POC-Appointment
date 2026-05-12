import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import type {
  DecisionStateDto,
  ModifyRequestDto,
} from '../../shared/models/decision-state.model';

/**
 * HTTP service for the Accept / Modify / Reject coding decision workflow (US_051).
 *
 * Endpoints:
 *   POST  /api/v1/coding-decisions/{decisionId}/accept   → AC-1
 *   PATCH /api/v1/coding-decisions/{decisionId}/modify   → AC-2
 *   POST  /api/v1/coding-decisions/{decisionId}/reject   → AC-3
 *
 * Agreement-rate data is recorded by the backend on each call (AIR-007).
 * No additional FE instrumentation is required beyond dispatching the correct verb.
 */
@Injectable({ providedIn: 'root' })
export class CodingDecisionService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/coding-decisions';

  /**
   * Accept an AI coding suggestion as-is (AC-1).
   * Records the original code and clinician identity in the audit trail.
   */
  accept(decisionId: string): Observable<DecisionStateDto> {
    return this.http.post<DecisionStateDto>(
      `${this.base}/${decisionId}/accept`,
      { decisionId },
    );
  }

  /**
   * Save a modified code replacing the AI suggestion (AC-2).
   * Stores original + final values with "Modified from AI suggestion" audit record.
   */
  modify(decisionId: string, req: ModifyRequestDto): Observable<DecisionStateDto> {
    return this.http.patch<DecisionStateDto>(
      `${this.base}/${decisionId}/modify`,
      req,
    );
  }

  /**
   * Reject an AI coding suggestion (AC-3).
   * Marks the suggestion as rejected and requires manual code entry via SCR-018.
   */
  reject(decisionId: string): Observable<DecisionStateDto> {
    return this.http.post<DecisionStateDto>(
      `${this.base}/${decisionId}/reject`,
      { decisionId },
    );
  }
}
