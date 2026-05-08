import { DocumentCategory } from './document-category.enum';

/**
 * A single row in the document library list.
 * Maps from the BE DocumentListItemDto returned by GET /api/v1/documents.
 */
export interface DocumentListItem {
  /** Backend GUID for the document. */
  documentId: string;
  /** Staff/patient-assigned display name (separate from the original storage filename). */
  displayName: string;
  /** Original storage filename as uploaded (read-only, never overwritten by rename). */
  originalFilename: string;
  /** Assigned category, null when uncategorized. */
  category: DocumentCategory | null;
  /** ISO-8601 string — upload timestamp. */
  uploadedAt: string;
  /** OCR pipeline status: Queued | Processing | Completed | Failed. */
  extractionStatus: string;
  /** Malware scan result: Scanning | Clean | ThreatDetected | PendingScan. */
  scanResult: string;
  /** True when the document has been soft-deleted (is_deleted = true). */
  isDeleted: boolean;
  /** ISO-8601 string when soft-deleted, null for active documents. */
  deletedAt: string | null;
}
