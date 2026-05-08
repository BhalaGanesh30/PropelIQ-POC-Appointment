/**
 * Response from POST /api/v1/staff-bookings (EP-004 US_035 AC-2).
 *
 * AC-2: Confirmed booking is attributed to the acting staff user (staffActorId)
 *       in the audit log so the booking is never anonymous.
 */
export interface StaffBookingResponse {
  /** UUID of the newly created booking record. */
  bookingId: string;
  /** UUID of the appointment entity linked to this booking. */
  appointmentId: string;
  /** Deep link to the booking confirmation page (optional). */
  confirmationUrl?: string;
  /** UUID of the staff user who performed the booking (AC-2 audit attribution). */
  staffActorId: string;
}
