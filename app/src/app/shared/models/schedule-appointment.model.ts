/**
 * SCR-026 Daily Schedule — appointment block displayed in the time-grid.
 *
 * AC-1: All appointments for the selected date are rendered in chronological order.
 * AC-3: patientName, appointmentType, startTime, duration are printed per the print layout.
 */
export interface ScheduleAppointment {
  /** UUID of the appointment (app.appointments.id). */
  appointmentId: string;
  /** Display name of the patient (first + last). */
  patientName: string;
  /** Colour-coding key: 'Scheduled' | 'WalkIn' | 'Override'. */
  appointmentType: 'Scheduled' | 'WalkIn' | 'Override';
  /**
   * ISO-8601 date-time string (e.g. "2026-05-06T09:30:00").
   * Used to calculate grid-row position.
   */
  startTime: string;
  /** Duration in minutes (e.g. 15 / 30 / 60). */
  duration: number;
  /** Current appointment status. */
  status: 'Confirmed' | 'CheckedIn' | 'InProgress' | 'Completed' | 'Cancelled';
  /** Optional provider or clinician name for display. */
  providerName?: string;
  /** Optional room or location string. */
  location?: string;
}
