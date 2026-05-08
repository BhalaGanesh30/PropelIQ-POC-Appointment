/**
 * Request body for POST /api/v1/scheduling/override (EP-004 US_034 AC-2).
 *
 * FR-SO-004: Staff override of scheduling constraints requires a mandatory
 * reason that is stored in the immutable audit record.
 */
export interface OverrideRequest {
  /** UUID of the appointment for which the constraint is being overridden. */
  appointmentId: string;

  /** Machine-readable type of the violated constraint (e.g., "SameDayWindow", "LateCancellation"). */
  constraintType: string;

  /**
   * Staff-provided justification text (1–500 characters).
   * Server validates required + maxLength(500).
   */
  reason: string;

  /**
   * The scheduling action being overridden
   * (e.g., "Cancel", "Reschedule", "Book", "SameDayBooking").
   */
  action: string;
}
