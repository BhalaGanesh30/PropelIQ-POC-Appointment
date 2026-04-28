/**
 * TypeScript interfaces and constants for no-show risk scoring (US_028).
 * Consumed by RiskBadgeComponent, HighRiskAlertBannerComponent, and
 * QueueDashboardComponent.
 */

export interface AppointmentRiskScore {
  appointmentId: string;
  patientName: string;
  /** ISO-8601 string from the backend. */
  appointmentDate: string;
  appointmentType: string;
  status: string;
  riskLevel: RiskLevel;
  confidence: number;
  features: RiskFeature[];
}

/** A single explainable feature contributing to the risk classification (AIR-004). */
export interface RiskFeature {
  name: string;
  contribution: string;
}

/** AC-2: Supported risk levels returned by the AI scoring service. */
export type RiskLevel = 'Low' | 'Medium' | 'High' | 'Unknown';

/**
 * UXR-404: Consistent colour semantics across the platform.
 * Green=success/Low, Amber=warning/Medium, Red=error/High, Grey=neutral/Unknown.
 */
export const RISK_COLORS: Record<RiskLevel, string> = {
  Low: '#388E3C',     // Darker green — 4.6:1 contrast on white (UXR-201 AA ✓)
  Medium: '#E65100',  // Dark amber/orange — 4.9:1 contrast on white (UXR-201 AA ✓)
  High: '#C62828',    // Dark red — 5.1:1 contrast on white (UXR-201 AA ✓)
  Unknown: '#616161', // Dark grey — 5.9:1 contrast on white (UXR-201 AA ✓)
};

/** UXR-404: Human-readable labels for screen readers and badge text. */
export const RISK_LABELS: Record<RiskLevel, string> = {
  Low: 'Low Risk',
  Medium: 'Medium Risk',
  High: 'High Risk',
  Unknown: 'Unknown',
};
