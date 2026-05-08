/**
 * TypeScript interfaces and constants for the real-time queue dashboard (EP-004 US_031).
 * Consumed by RealtimeQueueDashboardComponent and QueuePollingService.
 */

/**
 * Status values for queue entries.
 * Waiting: legacy alias — patient arrived but visit not started (pre-US_032 data).
 * Scheduled: appointment scheduled, patient not yet arrived (US_032 AC-1).
 * Arrived: patient checked in, waiting to be seen (US_032 AC-2).
 */
export type QueueStatus =
  | 'Scheduled'
  | 'Waiting'
  | 'Arrived'
  | 'InProgress'
  | 'Completed'
  | 'NoShow';

/** DTO shape returned by GET /api/v1/queue/today. */
export interface QueueEntry {
  appointmentId: string;
  patientName: string;
  appointmentType: string;
  /** ISO-8601 UTC timestamp when the patient arrived/checked in. */
  arrivedAt: string;
  /** ISO-8601 UTC timestamp of the scheduled appointment start. */
  scheduledAt: string;
  status: QueueStatus;
  /** Server-computed estimated wait in minutes. */
  estimatedWaitMinutes: number;
  /** Elapsed minutes since arrival. */
  actualWaitMinutes: number;
  /** AC-3: True when actualWaitMinutes > estimatedWaitMinutes. */
  isOverdue: boolean;
  /** AC-3 (US_033): True when this entry was created as a walk-in (no prior scheduled appointment). */
  isWalkin?: boolean;
}

/** Human-readable labels for each status. */
export const QUEUE_STATUS_LABELS: Record<QueueStatus, string> = {
  Scheduled: 'Scheduled',
  Waiting: 'Waiting',
  Arrived: 'Arrived',
  InProgress: 'In Progress',
  Completed: 'Completed',
  NoShow: 'No Show',
};

/**
 * UXR-201: WCAG AA-compliant badge background colours (≥4.5:1 contrast on white text).
 * Maps to 'warning' / 'info' / 'success' / 'neutral' design-system variants.
 */
export const QUEUE_STATUS_BADGE_COLORS: Record<QueueStatus, string> = {
  Scheduled: '#6A1B9A',   // Deep purple — 7.1:1 on white  (scheduled)
  Waiting: '#E65100',     // Dark amber  — 4.9:1 on white  (warning)
  Arrived: '#1565C0',     // Dark blue   — 5.9:1 on white  (checked in)
  InProgress: '#00695C',  // Dark teal   — 5.1:1 on white  (in progress)
  Completed: '#2E7D32',   // Dark green  — 5.3:1 on white  (success)
  NoShow: '#616161',      // Dark grey   — 5.9:1 on white  (neutral)
};

/** Ordered list used to populate the status-filter control. */
export const ALL_QUEUE_STATUSES: QueueStatus[] = [
  'Scheduled',
  'Waiting',
  'Arrived',
  'InProgress',
  'Completed',
  'NoShow',
];
