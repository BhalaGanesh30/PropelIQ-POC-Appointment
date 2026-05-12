import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DisclosureRequest,
  SubmitDisclosureRequest,
  SubmitDisclosureResponse,
} from './models/disclosure.models';

/**
 * Patient-facing HTTP client for the disclosure request API (US_057, AC-2).
 *
 * Base URL: /api/v1/patients/me/disclosure-requests
 * Auth: attached by the app-wide HTTP auth interceptor (Bearer JWT).
 * Backend enforces PatientOnly policy — never call from staff screens.
 */
@Injectable({ providedIn: 'root' })
export class DisclosureApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/patients/me/disclosure-requests';

  /**
   * Submits a new disclosure request for the authenticated patient (AC-2).
   * Returns 201 Created with the new request ID and initial status "Submitted".
   */
  submit(req: SubmitDisclosureRequest): Observable<SubmitDisclosureResponse> {
    return this.http.post<SubmitDisclosureResponse>(this.base, req);
  }

  /** Lists all disclosure requests for the authenticated patient (most-recent first). */
  list(): Observable<DisclosureRequest[]> {
    return this.http.get<DisclosureRequest[]>(this.base);
  }

  /** Returns the status and metadata for a specific disclosure request. */
  getStatus(id: string): Observable<DisclosureRequest> {
    return this.http.get<DisclosureRequest>(`${this.base}/${id}`);
  }

  /**
   * Downloads the compiled disclosure report JSON using the HMAC-signed token
   * delivered to the patient via email (AC-3, edge case 1).
   * Returns a Blob for local save-as / display.
   */
  downloadReport(id: string, token: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/download`, {
      params: { token },
      responseType: 'blob',
    });
  }
}
