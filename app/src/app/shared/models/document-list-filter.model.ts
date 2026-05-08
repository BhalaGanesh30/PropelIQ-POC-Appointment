import { DocumentCategory } from './document-category.enum';

/**
 * Query parameters for GET /api/v1/documents.
 * All fields are optional — omitted values are ignored by the API.
 */
export interface DocumentListFilter {
  /** Restrict to a specific category; null means all categories. */
  category: DocumentCategory | null;
  /** ISO-8601 date string for the lower bound of uploadedAt. */
  dateFrom: string | null;
  /** ISO-8601 date string for the upper bound of uploadedAt. */
  dateTo: string | null;
  /** OCR pipeline status filter; null means all statuses. */
  status: string | null;
  /** When true, includes soft-deleted documents (admin trash view, AC-4). */
  includeDeleted: boolean;
}
