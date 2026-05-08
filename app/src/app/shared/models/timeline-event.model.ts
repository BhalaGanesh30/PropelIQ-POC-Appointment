/**
 * A single clinical timeline event (US_048 AC-1).
 * Returned by GET /api/v1/patients/{id}/timeline.
 */
export interface TimelineEventDto {
  /** Unique event identifier. */
  eventId: string;
  /** Machine-readable event type: e.g. "medication_started", "diagnosis_recorded", "document_uploaded". */
  eventType: string;
  /** Category discriminator: "medication" | "allergy" | "diagnosis" | "document" */
  category: 'medication' | 'allergy' | 'diagnosis' | 'document' | string;
  /** Human-readable description of the event. */
  description: string;
  /** ISO-8601 timestamp of when the clinical event occurred. */
  eventDate: string;
  /** Patient GUID for reference. */
  patientId: string;
}

/** Response envelope for GET /api/v1/patients/{id}/timeline. */
export interface TimelineResponseDto {
  events: TimelineEventDto[];
  totalCount: number;
}

/** Query parameters accepted by the timeline API (AC-2, AC-3, Edge Case 2). */
export interface TimelineQueryParams {
  category?: string;
  dateFrom?: string;
  dateTo?: string;
}

/** A grouped collection of events for a single calendar year (Edge Case 2). */
export interface TimelineYearGroup {
  year: number;
  events: TimelineEventDto[];
}
