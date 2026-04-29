/**
 * Query parameters for GET /api/v1/appointmenthistory.
 * AC-1: sorted date descending (server-side).
 * AC-2: optional status filter.
 * AC-3: optional date range filter.
 * Edge case: default page size 20 for pagination.
 */
export interface AppointmentHistoryFilter {
  status?: string;
  /** ISO-8601 string — start of date range (AC-3). */
  dateFrom?: string;
  /** ISO-8601 string — end of date range (AC-3). */
  dateTo?: string;
  page: number;
  pageSize: number;
}

/**
 * A single appointment in the history list returned by
 * GET /api/v1/appointmenthistory.
 * Maps from the BE AppointmentHistoryItem DTO.
 */
export interface AppointmentHistoryItem {
  id: string;
  /** ISO-8601 DateTimeOffset — appointment date and time (AC-1 sort key). */
  scheduledAt: string;
  durationMinutes: number;
  appointmentType: string;
  status: string;
  providerName: string | null;
  location: string | null;
  confirmationCode: string;
  /** True when an intake record has been submitted for this appointment. */
  hasIntakeRecord: boolean;
}

/**
 * Paginated response from GET /api/v1/appointmenthistory.
 * Edge case: empty history returns items=[] and totalCount=0.
 */
export interface AppointmentHistoryResponse {
  items: AppointmentHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/** Valid status values accepted by the filter (AC-2). */
export const APPOINTMENT_STATUSES = [
  'Confirmed',
  'Completed',
  'Cancelled',
  'NoShow',
  'Rescheduled',
] as const;

export type AppointmentStatus = (typeof APPOINTMENT_STATUSES)[number];
