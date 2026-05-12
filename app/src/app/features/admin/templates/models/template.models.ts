/**
 * TypeScript interfaces for the SCR-024 Template Editor screen (US_062).
 *
 * Mirrors the JSON shapes returned by the backend TemplatesController
 * at /api/v1/admin/templates.
 */

// ── List / detail ────────────────────────────────────────────────────────────

export interface TemplateListItem {
  id: string;
  name: string;
  type: 'HTML' | 'SMS';
  description: string;
  currentVersionNumber: number;
  lastModifiedUtc: string;
  lastModifiedByName: string;
}

export interface TemplateDetail {
  id: string;
  name: string;
  type: 'HTML' | 'SMS';
  description: string;
  currentVersion: TemplateVersionItem;
  createdAt: string;
}

export interface TemplateVersionItem {
  id: string;
  versionNumber: number;
  content: string;
  subject: string | null;
  isActive: boolean;
  createdAtUtc: string;
  createdByName: string;
  restoredFromVersionId: string | null;
}

// ── Requests ─────────────────────────────────────────────────────────────────

export interface SaveTemplateRequest {
  content: string;
  subject?: string;
}

// ── Responses ────────────────────────────────────────────────────────────────

export interface PreviewResponse {
  renderedHtml: string;
  renderedSubject: string | null;
  /** Present only when template type is SMS (edge case 1). */
  smsInfo: SmsInfo | null;
}

/** SMS segment metadata returned by the preview endpoint (edge case 1). */
export interface SmsInfo {
  characterCount: number;
  isMultiPart: boolean;
  estimatedSegments: number;
}

/** Result from the /validate endpoint (AC-4, edge case 2). */
export interface TemplateValidationResult {
  isValid: boolean;
  /** Merge-field tokens that are completely unknown to the registry. */
  invalidPlaceholders: string[];
  /** Tokens that were valid but have since been deactivated in the registry. */
  orphanedPlaceholders: string[];
}

export interface TemplatePagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
