/**
 * Walk-in models for EP-004 US_033 (Walk-In Creation and Patient Registration Conversion).
 * Used by WalkinService and WalkinRegistrationComponent.
 */

/** Request body for POST /api/v1/walkins. */
export interface CreateWalkinRequest {
  /** Patient display name (required, max 200 chars). */
  patientName: string;
  /** Patient phone number (optional). */
  phone?: string;
  /** Reason for visit (required, max 500 chars). */
  visitReason: string;
  /**
   * Existing patient UUID to link to.
   * Null when creating a new temporary record or converting inline (AC-4).
   */
  existingPatientId?: string;
  /** AC-2: When true, creates a full patient account alongside the walk-in. */
  convertToPatient: boolean;
  /** Patient date of birth — required when convertToPatient is true (AC-2). */
  dateOfBirth?: string;
  /** Patient email — required when convertToPatient is true (AC-2). */
  email?: string;
}

/**
 * Response from POST /api/v1/walkins (AC-1).
 * Returns the new queue entry with the assigned queue position.
 */
export interface WalkinResponse {
  walkinId: string;
  appointmentId: string;
  patientName: string;
  visitReason: string;
  /** 1-based position in today's queue. */
  queuePosition: number;
  /** Estimated wait in minutes at the time of creation. */
  estimatedWaitMinutes: number;
  /** True when the clinic is at or above capacity threshold (Edge Case 2). */
  atCapacity: boolean;
}
