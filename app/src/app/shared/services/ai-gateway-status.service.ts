import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import type { AiGatewayStatusDto } from '../models/ai-gateway-status.dto';

/**
 * HTTP service for the AI gateway circuit breaker status endpoint (US_053, AC-2, AC-3).
 *
 * Endpoint: `GET /api/v1/ai-gateway/status`
 *
 * Provided in root so the single instance is shared by `AiGatewayStatusFacade`
 * across the entire application lifetime.
 */
@Injectable({ providedIn: 'root' })
export class AiGatewayStatusService {
  private readonly http = inject(HttpClient);

  /**
   * Fetches the current circuit breaker state from the AI gateway.
   * Returns a cold Observable — the caller decides when to subscribe.
   */
  getStatus(): Observable<AiGatewayStatusDto> {
    return this.http.get<AiGatewayStatusDto>('/api/v1/ai-gateway/status');
  }
}
