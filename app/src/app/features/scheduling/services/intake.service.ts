import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  IntakeAssistRequest,
  IntakeAssistResponse,
  IntakeDraftResponse,
  SaveDraftRequest,
  SaveDraftResponse,
  SubmitIntakeRequest,
  SubmitIntakeResponse,
} from '../models/intake.model';

@Injectable({ providedIn: 'root' })
export class IntakeApiService {
  private readonly baseUrl = `${environment.apiBaseUrl}/intake`;
  private readonly http = inject(HttpClient);

  /**
   * Autosave partial form data on blur (AC-2).
   * Creates or updates the draft for this patient + slot combination.
   */
  saveDraft(request: SaveDraftRequest): Observable<SaveDraftResponse> {
    return this.http.put<SaveDraftResponse>(`${this.baseUrl}/draft`, request);
  }

  /**
   * Retrieve saved draft to resume the form (AC-3).
   * Returns the draft for the given slot, or the most recent draft when slotId is omitted.
   * The server responds with 204 when no draft exists — callers should handle the null case.
   */
  getDraft(slotId?: string): Observable<IntakeDraftResponse> {
    const params = slotId ? new HttpParams().set('slotId', slotId) : {};
    return this.http.get<IntakeDraftResponse>(`${this.baseUrl}/draft`, { params });
  }

  /**
   * Finalize and submit the intake form, attaching it to the booking (AC-4).
   */
  submitIntake(request: SubmitIntakeRequest): Observable<SubmitIntakeResponse> {
    return this.http.post<SubmitIntakeResponse>(`${this.baseUrl}/submit`, request);
  }

  /**
   * AI-assisted intake prefill — returns structured field suggestions from
   * a free-text symptom description (AC-1, AIR-005 fallback handled by server).
   */
  aiAssist(request: IntakeAssistRequest): Observable<IntakeAssistResponse> {
    return this.http.post<IntakeAssistResponse>(`${this.baseUrl}/ai-assist`, request);
  }
}
