/** Query parameters sent to GET /api/v1/appointments/slots */
export interface SlotSearchParams {
  dateFrom: string; // ISO date string e.g. "2026-04-22"
  dateTo: string;   // ISO date string e.g. "2026-04-29"
  duration?: 15 | 30 | 60;
  type?: AppointmentType;
}

export type AppointmentType = 'General' | 'Specialist' | 'FollowUp' | 'Urgent';

/** Top-level response from GET /api/v1/appointments/slots */
export interface SlotSearchResponse {
  days: SlotGroup[];
  totalAvailableSlots: number;
  hasResults: boolean;
}

/** Slots for a single calendar date */
export interface SlotGroup {
  date: string; // ISO date string e.g. "2026-04-22"
  slots: SlotDto[];
}

/** Individual bookable time slot */
export interface SlotDto {
  id: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  type: string;
  providerName: string | null;
  location: string | null;
  availableCapacity: number;
}
