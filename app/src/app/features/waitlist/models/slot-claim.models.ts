/**
 * Slot claim page models for US_030 task_002 (SCR-008 claim flow).
 *
 * The HMAC-signed claim URL from the slot alert email/SMS takes the form:
 *   {base}/waitlist/{entryId}/claim?token={rawToken}
 * The frontend resolves the token via GET /api/v1/waitlist/claim-details.
 */

/** Response from GET /api/v1/waitlist/claim-details (AC-2). */
export interface SlotClaimDetails {
  /** UUID of the WaitlistEntry. */
  waitlistEntryId: string;
  /** ISO 8601 UTC — converted to browser timezone client-side (edge case 2). */
  slotDateTime: string;
  slotType: string;
  providerName: string | null;
  durationMinutes: number;
  /** UTC expiry for the countdown timer (AC-2). */
  expiresAtUtc: string;
  /** 'Offered' | 'Claimed' | 'Expired' */
  status: string;
}

/** Success response from POST /api/v1/waitlist/{id}/claim (AC-3). */
export interface ClaimResult {
  appointmentId: string;
  confirmationCode: string;
  appointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
  location: string | null;
}

/** Countdown urgency level (UXR-112, UXR-404). */
export type CountdownUrgency = 'normal' | 'warning' | 'critical' | 'expired';

/**
 * Color mapping per urgency level (SCR-008 / UXR-404).
 *   normal   — green:  > 1 hour remaining
 *   warning  — amber:  30 min – 1 hour
 *   critical — red:    < 30 min
 *   expired  — grey:   0
 */
export const URGENCY_COLORS: Record<CountdownUrgency, string> = {
  normal:   '#2E7D32',   // Material green-800 — WCAG AA on white
  warning:  '#E65100',   // Material deep-orange-900 — WCAG AA on white
  critical: '#C62828',   // Material red-900 — WCAG AA on white
  expired:  '#616161',   // Material grey-700 — WCAG AA on white
};
