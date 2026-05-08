/**
 * Request payload for POST /api/v1/staff-bookings (EP-004 US_035 AC-1, AC-2).
 *
 * AC-1: Staff selects an existing patient via search.
 * AC-2: Staff may optionally create a new patient profile inline.
 * AC-3: Visit reason and optional override reason are required when a
 *       scheduling constraint was bypassed (Edge Case 1).
 */

/** Inline new-patient form data — used when the patient is not found in search. */
export interface InlinePatientForm {
  firstName: string;
  lastName: string;
  /** E.164 / local-format phone number (validated server-side). */
  phone: string;
  /** ISO-8601 date of birth (YYYY-MM-DD). */
  dateOfBirth: string;
  email?: string;
}

/** Body for POST /api/v1/staff-bookings. */
export interface StaffBookingRequest {
  /** UUID of an existing patient — mutually exclusive with newPatient. */
  patientId?: string;
  /** Slot UUID from GET /api/v1/appointments/slots. */
  slotId: string;
  /** Visit reason text (max 500 chars). */
  visitReason: string;
  /**
   * Override reason text (max 300 chars).
   * Required when the slot has a detected conflict that the staff acknowledged.
   */
  overrideReason?: string;
  /** Inline new-patient data — mutually exclusive with patientId. */
  newPatient?: InlinePatientForm;
}
