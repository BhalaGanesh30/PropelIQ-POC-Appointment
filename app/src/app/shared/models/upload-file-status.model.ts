/**
 * Scan and processing status values returned by
 * `GET /api/v1/documents/{id}/status` (EP-006 US_040).
 *
 * Terminal states: Clean, ThreatDetected, Completed, Failed.
 * Non-terminal states: Scanning, PendingScan, Processing.
 */
export type ScanResult =
  | 'Scanning'
  | 'Clean'
  | 'ThreatDetected'
  | 'PendingScan'
  | 'Processing'
  | 'Completed'
  | 'Failed';

/**
 * Per-file state tracked by `DocumentUploadComponent`.
 * Created on file selection and updated as upload, scanning, and OCR progress.
 */
export interface UploadFileStatus {
  /** Unique ID returned by the API after a successful upload. Null until upload completes. */
  documentId: string | null;
  /** Original file name. */
  fileName: string;
  /** File size in bytes (validated ≤ 10 MB client-side before upload). */
  fileSize: number;
  /** Upload progress 0–100, updated from HttpClient progress events (UXR-505). */
  uploadProgress: number;
  /** Current scan / processing status returned by polling. */
  scanResult: ScanResult;
  /** True while the upload HTTP request is in flight. */
  isUploading: boolean;
  /** Non-null when the upload fails with an HTTP error. */
  uploadError: string | null;
  // ── OCR / extraction fields (US_041) ────────────────────────────────────
  /** Current OCR extraction pipeline status (US_041 AC-1 → AC-4). */
  extractionStatus: string | null;
  /** First 500 chars of extracted text, available when extractionStatus = 'Completed'. */
  extractedTextPreview: string | null;
  /** True when OCR produced low-confidence output requiring manual review (Edge Case 1). */
  needsManualReview: boolean;
  /** Number of OCR retry attempts made (max 3, AC-4). */
  retryCount: number;
  /** True while a retryOcr request is in flight (UXR-501). */
  isRetrying: boolean;
}

/**
 * Returns true when the `ScanResult` is terminal and polling should stop.
 * Satisfies Edge Case 1 (PendingScan is NOT terminal — polling must continue).
 */
export function isFinalScanResult(status: ScanResult): boolean {
  return (
    status === 'Clean' ||
    status === 'ThreatDetected' ||
    status === 'Completed' ||
    status === 'Failed'
  );
}
