/**
 * TypeScript models for the SCR-019 System Configuration screen (US_059).
 *
 * Mirrors the backend's ConfigurationCategory enum and ConfigurationSnapshot /
 * ConfigurationVersionDto response shapes.
 */

export type ConfigCategory =
  | 'SlotTemplates'
  | 'ReminderRules'
  | 'SessionPolicy'
  | 'CommunicationTemplates';

/** Current active configuration snapshot returned by GET /api/v1/admin/config/{category}. */
export interface ConfigSnapshot {
  versionId: string;
  versionNumber: number;
  category: ConfigCategory;
  values: Record<string, unknown>;
  updatedAtUtc: string;
  updatedByName: string;
}

/** A single entry in the version history list (AC-3). */
export interface ConfigVersion {
  versionId: string;
  versionNumber: number;
  category: ConfigCategory;
  changedAtUtc: string;
  changedByAdminId: string;
  changedByName: string;
  values: Record<string, unknown>;
  previousValues: Record<string, unknown> | null;
  restoredFromVersionId: string | null;
}

/** Result returned by PUT update and POST restore operations. */
export interface ConfigUpdateResult {
  versionId: string;
  versionNumber: number;
}

/** Category metadata for sidebar navigation and accordion headers. */
export interface ConfigCategoryMeta {
  key: ConfigCategory;
  label: string;
  icon: string;
}

export const CONFIG_CATEGORIES: ConfigCategoryMeta[] = [
  { key: 'SlotTemplates',         label: 'Slot Templates',         icon: 'schedule' },
  { key: 'ReminderRules',         label: 'Reminder Rules',         icon: 'notifications' },
  { key: 'SessionPolicy',         label: 'Session Policy',         icon: 'security' },
  { key: 'CommunicationTemplates', label: 'Communication Templates', icon: 'email' },
];
