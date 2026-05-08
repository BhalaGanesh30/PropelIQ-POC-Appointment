/**
 * OCR/extraction pipeline status values returned by
 * `GET /api/v1/documents/{id}/status` (EP-006 US_041).
 *
 * Terminal states: Completed, Failed.
 * Non-terminal states: Queued, Processing.
 */
export enum ExtractionStatus {
  Queued     = 'Queued',
  Processing = 'Processing',
  Completed  = 'Completed',
  Failed     = 'Failed',
}

/** Returns true when the extraction status is terminal and polling should stop. */
export function isFinalExtractionStatus(status: ExtractionStatus | string): boolean {
  return status === ExtractionStatus.Completed || status === ExtractionStatus.Failed;
}
