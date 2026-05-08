import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import type {
  ConflictAlertsResponseDto,
} from '../../shared/models/conflict-alert.model';

/**
 * Angular service wrapping the conflict detection API (EP-007 US_046 FR-CA-003).
 *
 * Endpoints consumed:
 *   GET  /api/v1/patients/{id}/conflicts        — fetch deduplicated conflicts for patient
 *   POST /api/v1/conflicts/{conflictId}/acknowledge — record clinician acknowledgment (AC-4)
 */
@Injectable({ providedIn: 'root' })
export class ConflictAlertsService {
  private readonly http = inject(HttpClient);

  /**
   * Fetches all deduplicated conflict alerts for the given patient.
   * Response includes `rulesStale` flag (Edge Case 1).
   */
  getConflicts(patientId: string): Observable<ConflictAlertsResponseDto> {
    return this.http.get<ConflictAlertsResponseDto>(
      `/api/v1/patients/${patientId}/conflicts`,
    );
  }

  /**
   * Records a clinician acknowledgment for the given conflict (AC-4).
   * The server logs the acknowledging user's identity and timestamp in the audit trail.
   */
  acknowledgeConflict(conflictId: string): Observable<void> {
    return this.http.post<void>(
      `/api/v1/conflicts/${conflictId}/acknowledge`,
      {},
    );
  }
}
