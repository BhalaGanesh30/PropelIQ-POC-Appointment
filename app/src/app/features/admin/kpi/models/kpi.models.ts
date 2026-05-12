/**
 * KPI metric type identifiers matching the backend C# enum (US_060).
 */
export type KpiMetricType =
  | 'NoShowRate'
  | 'AppointmentUtilization'
  | 'AverageWaitTime'
  | 'BookingVolume';

/** Export format requested for AC-3. */
export type KpiExportFormat = 'Png' | 'Pdf';

// ── Response shapes (mirror backend DTOs) ────────────────────────────────────

export interface KpiCardValue {
  metric: KpiMetricType;
  value: number;
  previousPeriodValue: number | null;
  changePercent: number | null;
}

/** AC-1 summary response — four KPI cards. Edge case 1: isStale flag. */
export interface KpiSummaryResponse {
  cards: KpiCardValue[];
  computedAtUtc: string;
  isStale: boolean;
}

export interface KpiTimeSeriesPoint {
  date: string;
  value: number;
}

/** AC-2 time-series response for chart rendering. */
export interface KpiTimeSeriesResponse {
  metric: KpiMetricType;
  points: KpiTimeSeriesPoint[];
  computedAtUtc: string;
  isStale: boolean;
}

// ── Request shapes ────────────────────────────────────────────────────────────

export interface KpiExportRequest {
  range: { from: string; to: string };
  format: KpiExportFormat;
}

// ── Display metadata ──────────────────────────────────────────────────────────

export interface KpiMetricConfig {
  key: KpiMetricType;
  label: string;
  icon: string;
  unit: string;
  /** Hex color for chart series and icon (semantic palette, UXR-404). */
  color: string;
}

export const KPI_METRIC_CONFIG: KpiMetricConfig[] = [
  {
    key: 'NoShowRate',
    label: 'No-Show Rate',
    icon: 'person_off',
    unit: '%',
    color: '#E53935',
  },
  {
    key: 'AppointmentUtilization',
    label: 'Appointment Utilization',
    icon: 'event_available',
    unit: '%',
    color: '#43A047',
  },
  {
    key: 'AverageWaitTime',
    label: 'Average Wait Time',
    icon: 'schedule',
    unit: 'min',
    color: '#1E88E5',
  },
  {
    key: 'BookingVolume',
    label: 'Booking Volume',
    icon: 'book_online',
    unit: '',
    color: '#8E24AA',
  },
];
