/**
 * Slot availability models for EP-004 US_035 Staff-Assisted Booking (SCR-027).
 * Returned by GET /api/v1/appointments/slots and /api/v1/appointments/conflict-check.
 */

/** Result of a conflict-check for a given patient + slot combination. */
export interface ConflictCheck {
  /** Whether the patient has an existing booking that overlaps the requested slot. */
  hasConflict: boolean;
  /** UUID of the conflicting appointment (present only when hasConflict is true). */
  existingAppointmentId?: string;
  /** ISO-8601 datetime of the conflicting appointment. */
  existingDateTime?: string;
  /** Provider name of the conflicting appointment. */
  existingProvider?: string;
}

/** A single bookable (or unavailable) appointment slot. */
export interface SlotResult {
  /** Slot UUID used when submitting the booking request. */
  slotId: string;
  /** ISO-8601 datetime of the slot start. */
  dateTime: string;
  /** Duration in minutes. */
  duration: number;
  /** Whether the slot is still available. */
  available: boolean;
  /** Provider display name (optional). */
  providerName?: string;
  /** Conflict details populated only after checkConflict is called for this slot. */
  conflictDetails?: ConflictCheck;
}
