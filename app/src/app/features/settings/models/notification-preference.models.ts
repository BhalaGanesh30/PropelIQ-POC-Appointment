/**
 * TypeScript interfaces and constants for notification preferences (US_029, SCR-009).
 * Consumed by NotificationPreferencesApiService and NotificationPreferencesComponent.
 */

/** Valid reminder offset keys matched by the server-side validator. */
export type ReminderTiming = '7d' | '2d' | '1d' | '2h';

/** Outbound payload for PUT /api/v1/notification-preferences (AC-2). */
export interface NotificationPreferenceDto {
  emailEnabled: boolean;
  smsEnabled: boolean;
  reminderTimings: ReminderTiming[];
}

/**
 * Response shape returned by GET and PUT endpoints.
 * `hasPhoneNumber` drives the SMS phone-number validation guard (edge case 1).
 */
export interface NotificationPreferenceResponse extends NotificationPreferenceDto {
  hasPhoneNumber: boolean;
}

/** UXR-402: Human-readable labels for the reminder timing checkboxes. */
export const REMINDER_TIMING_LABELS: Record<ReminderTiming, string> = {
  '7d': '7 days before',
  '2d': '2 days before',
  '1d': '1 day before',
  '2h': '2 hours before',
};

/** All supported timing offsets in display order. */
export const ALL_TIMINGS: ReminderTiming[] = ['7d', '2d', '1d', '2h'];
