/**
 * Response body returned by `POST /api/v1/documents/upload` (EP-006 US_040 AC-2).
 *
 * The `scanResult` in the upload response reflects the initial state immediately after
 * the file is accepted.  Clients must poll `GET /api/v1/documents/{documentId}/status`
 * to track scan and OCR progress to a terminal state.
 */
export interface DocumentUploadResponse {
  /** Unique identifier for the uploaded document. */
  documentId: string;
  /**
   * Initial scan result at upload time.
   * Typically 'Scanning' or 'PendingScan' (Edge Case 1 — scanner unavailable).
   * The final outcome is returned by the status polling endpoint.
   */
  scanResult: string;
  /** Human-readable message, e.g. "File accepted for scanning." */
  message: string;
}

/**
 * Response body returned by `GET /api/v1/documents/{id}/status` (EP-006 US_040/US_041).
 * Used to drive the status badge on each uploaded file row.
 */
export interface DocumentStatusResponse {
  documentId: string;
  scanResult: string;
  processingStatus: string;
  message: string;
  /** OCR extraction pipeline status (US_041): Queued | Processing | Completed | Failed */
  extractionStatus: string;
  /** First 500 chars of extracted text (available when extractionStatus = 'Completed') */
  extractedText: string | null;
  /** True when OCR confidence is below threshold — manual review needed (Edge Case 1) */
  needsManualReview: boolean;
}
