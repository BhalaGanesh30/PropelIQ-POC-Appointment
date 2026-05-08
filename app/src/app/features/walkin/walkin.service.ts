import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { CreateWalkinRequest, WalkinResponse } from '../../shared/models/walkin-entry.model';

/**
 * HTTP client for the walk-in creation endpoint (EP-004 US_033 AC-1, AC-2).
 * POST /api/v1/walkins — creates a queue entry for a walk-in patient.
 */
@Injectable({ providedIn: 'root' })
export class WalkinService {
  private readonly http = inject(HttpClient);

  /**
   * Creates a walk-in queue entry.
   * Returns the new entry with queue position and estimated wait time.
   */
  createWalkin(payload: CreateWalkinRequest): Observable<WalkinResponse> {
    return this.http.post<WalkinResponse>('/api/v1/walkins', payload);
  }
}
