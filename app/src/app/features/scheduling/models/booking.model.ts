/** POST /api/v1/bookings request body */
export interface CreateBookingRequest {
  slotId: string;
  intakeRecordId?: string;
}

/** POST /api/v1/bookings — success (HTTP 201) */
export interface BookingResponse {
  appointmentId: string;
  confirmationCode: string;
  /** ISO-8601 string */
  appointmentTime: string;
  durationMinutes: number;
  appointmentType: string;
  providerName: string | null;
  location: string | null;
  status: string;
  /** ISO-8601 string */
  bookedAt: string;
}

/** POST /api/v1/bookings — concurrent conflict (HTTP 409) */
export interface SlotConflictResponse {
  message: string;
  nextAvailableSlotId: string | null;
  /** ISO-8601 string */
  nextAvailableTime: string | null;
}

/** Artifact type accepted by GET /api/v1/bookings/{id}/artifacts/{type} */
export type ArtifactType = 'pdf' | 'qr' | 'ics';
