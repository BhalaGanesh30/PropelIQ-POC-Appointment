/** POST /api/v1/waitlist request body (AC-1). */
export interface JoinWaitlistRequest {
  preferredDateStart: string;
  preferredDateEnd: string;
  preferredDurationMinutes: number;
  preferredAppointmentType: string;
}

/** Waitlist entry returned by GET /api/v1/waitlist and POST /api/v1/waitlist. */
export interface WaitlistEntry {
  id: string;
  status: 'Active' | 'Offered' | 'Claimed' | 'Expired' | 'Cancelled';
  preferredDateStart: string;
  preferredDateEnd: string;
  preferredDurationMinutes: number;
  preferredAppointmentType: string;
  offeredSlotId: string | null;
  offeredAt: string | null;
  /** ISO-8601 UTC timestamp — drives the countdown timer (UXR-112). */
  claimExpiresAt: string | null;
  /** FIFO position in the waitlist queue. */
  position: number;
  createdAt: string;
}

/** POST /api/v1/waitlist/{id}/claim success response (AC-3). */
export interface ClaimResponse {
  appointmentId: string;
  confirmationCode: string;
  appointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
  location: string | null;
}

/**
 * Urgency level for the countdown timer (UXR-112).
 * green  — > 1 hour remaining
 * amber  — 30 min – 1 hour remaining
 * red    — < 30 min remaining
 * expired — claim window has passed
 */
export type CountdownUrgency = 'green' | 'amber' | 'red' | 'expired';
