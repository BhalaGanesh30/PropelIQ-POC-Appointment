import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ClaimResult, SlotClaimDetails } from './models/slot-claim.models';

/**
 * HTTP client for the slot claim flow (US_030 task_002).
 *
 * GET  /api/v1/waitlist/claim-details?token=   — resolve HMAC token → slot details
 * POST /api/v1/waitlist/{id}/claim?token=      — claim an offered slot (AC-3)
 *
 * Authentication is attached by the HttpClient auth interceptor.
 */
@Injectable({ providedIn: 'root' })
export class SlotClaimApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/waitlist';

  /**
   * Resolves the HMAC-signed claim token to slot details and expiry timestamp.
   * The token was embedded in the slot alert email/SMS by the backend (AC-1).
   * Edge case 2: expiresAtUtc is returned in UTC; the countdown converts it
   * to browser timezone client-side.
   */
  getClaimDetails(token: string): Observable<SlotClaimDetails> {
    const params = new HttpParams().set('token', token);
    return this.http.get<SlotClaimDetails>(`${this.baseUrl}/claim-details`, { params });
  }

  /**
   * Claims the offered slot (AC-3).
   * Sends the HMAC token as a query param for server-side HMAC validation.
   * Returns booking confirmation details on success.
   * HTTP 410 = claim window expired (AC-4); HTTP 409 = concurrent claim race.
   */
  claimSlot(entryId: string, token: string): Observable<ClaimResult> {
    const params = new HttpParams().set('token', token);
    return this.http.post<ClaimResult>(
      `${this.baseUrl}/${encodeURIComponent(entryId)}/claim`,
      {},
      { params },
    );
  }
}
