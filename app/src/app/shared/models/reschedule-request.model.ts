/**
 * Payload sent to PUT /api/v1/schedule/reschedule when a staff member
 * drag-drops an appointment block to a new time slot (AC-2).
 *
 * overrideReason is mandatory — collected via OverrideReasonDialogComponent
 * before the API call is made (US_034 integration).
 */
export interface RescheduleRequest {
  /** UUID of the appointment being rescheduled. */
  appointmentId: string;
  /**
   * New start time as ISO-8601 date-time string (e.g. "2026-05-06T11:00:00").
   * Derived from the drop target row index in the time-grid.
   */
  newStartTime: string;
  /** Mandatory reason captured by OverrideReasonDialogComponent (AC-2). */
  overrideReason: string;
}
