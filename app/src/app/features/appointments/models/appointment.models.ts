/** Appointment list item returned by GET /api/v1/bookings */
export interface AppointmentListItem {
  id: string;
  confirmationCode: string;
  /** ISO-8601 string */
  appointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
  location: string | null;
  status: 'Confirmed' | 'Cancelled' | 'Completed' | 'NoShow' | 'Rescheduled';
  /** ISO-8601 string */
  bookedAt: string;
  /** Server-computed: status === 'Confirmed' && >24 h away for patients */
  canModify: boolean;
}

/** POST /api/v1/bookings/{id}/cancel request body */
export interface CancelRequest {
  overrideReason?: string;
}

/** POST /api/v1/bookings/{id}/cancel success response */
export interface CancelResponse {
  appointmentId: string;
  status: string;
  /** ISO-8601 string */
  cancelledAt: string;
}

/** POST /api/v1/bookings/{id}/reschedule request body */
export interface RescheduleRequest {
  newSlotId: string;
  overrideReason?: string;
}

/** POST /api/v1/bookings/{id}/reschedule success response */
export interface RescheduleResponse {
  appointmentId: string;
  confirmationCode: string;
  /** ISO-8601 string */
  newAppointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
  status: string;
  /** ISO-8601 string */
  rescheduledAt: string;
}

/** Query parameters for GET /api/v1/bookings */
export interface AppointmentFilter {
  startDate?: string;
  endDate?: string;
  status?: string;
  page: number;
  pageSize: number;
}

/** Generic paginated response envelope */
export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
