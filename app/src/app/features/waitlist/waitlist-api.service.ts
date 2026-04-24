import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ClaimResponse,
  JoinWaitlistRequest,
  WaitlistEntry,
} from './models/waitlist.models';

/**
 * HTTP client for the preferred-slot waitlist API (US_023).
 *
 * POST   /api/v1/waitlist              — joinWaitlist (AC-1)
 * GET    /api/v1/waitlist              — getEntries
 * POST   /api/v1/waitlist/{id}/claim   — claimSlot (AC-3)
 * DELETE /api/v1/waitlist/{id}         — cancelEntry
 *
 * Authentication is attached by the HttpClient auth interceptor — no manual
 * header manipulation required.
 */
@Injectable({ providedIn: 'root' })
export class WaitlistApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/waitlist';

  /** Join the waitlist with preferred slot parameters (AC-1). */
  joinWaitlist(request: JoinWaitlistRequest): Observable<WaitlistEntry> {
    return this.http.post<WaitlistEntry>(this.baseUrl, request);
  }

  /** Get the current patient's Active and Offered waitlist entries. */
  getEntries(): Observable<WaitlistEntry[]> {
    return this.http.get<WaitlistEntry[]>(this.baseUrl);
  }

  /** Claim an offered slot to create a confirmed appointment (AC-3). */
  claimSlot(entryId: string): Observable<ClaimResponse> {
    return this.http.post<ClaimResponse>(
      `${this.baseUrl}/${encodeURIComponent(entryId)}/claim`,
      {},
    );
  }

  /** Cancel (remove) a waitlist entry. */
  cancelEntry(entryId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/${encodeURIComponent(entryId)}`,
    );
  }
}
