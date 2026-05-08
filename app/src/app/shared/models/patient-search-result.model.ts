/**
 * Patient search result model for EP-004 US_033 (AC-4).
 * Returned by GET /api/v1/patients/search.
 */
export interface PatientSearchResult {
  /** Patient UUID. */
  id: string;
  /** Full display name. */
  name: string;
  /** ISO-8601 date of birth (YYYY-MM-DD). */
  dateOfBirth: string;
  /** Patient phone number (may be absent). */
  phone?: string;
  /** Patient email address. */
  email?: string;
}
