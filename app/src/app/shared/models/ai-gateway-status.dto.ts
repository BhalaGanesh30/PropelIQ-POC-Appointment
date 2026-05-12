/**
 * DTO for the AI gateway circuit breaker status response (US_053, AC-2, AC-3).
 *
 * Returned by `GET /api/v1/ai-gateway/status`.
 *
 * `circuitState`:
 *   - `'closed'`   — AI provider healthy; requests flow normally.
 *   - `'open'`     — Circuit tripped after 5+ consecutive failures; fallback active.
 *   - `'half-open'` — Probe request sent; waiting to confirm recovery.
 *
 * `fallbackActive` mirrors the circuit being `open` or `half-open`.
 * `lastTripAt` is the ISO 8601 timestamp when the circuit last tripped, or `null`.
 */
export interface AiGatewayStatusDto {
  circuitState: 'closed' | 'open' | 'half-open';
  fallbackActive: boolean;
  lastTripAt: string | null;
}
