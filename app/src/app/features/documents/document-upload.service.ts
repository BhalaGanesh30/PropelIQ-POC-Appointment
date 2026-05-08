import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEvent, HttpRequest } from '@angular/common/http';
import { Observable } from 'rxjs';

import type { DocumentUploadResponse, DocumentStatusResponse } from '../../shared/models/document-upload-response.model';

/**
 * Service wrapping the document upload, status, and OCR retry endpoints
 * (EP-006 US_040/US_041 FR-DM-001/FR-DM-002).
 *
 * Upload: POST /api/v1/documents/upload
 *   - Sends `multipart/form-data` with the file + patientId field.
 *   - Uses `reportProgress: true` so the caller can track upload progress via
 *     `HttpEventType.UploadProgress` events (UXR-505).
 *
 * Status: GET /api/v1/documents/{id}/status
 *   - Polled by the component until a terminal scan + extraction result is reached.
 *   - Returns `DocumentStatusResponse` with current scan and extraction states.
 *
 * Retry OCR: POST /api/v1/documents/{id}/retry-ocr
 *   - Triggers a manual re-queue of OCR processing for failed documents (US_041 AC-4).
 */
@Injectable({ providedIn: 'root' })
export class DocumentUploadService {
  private readonly http = inject(HttpClient);

  /**
   * Uploads a file for the given patient.
   * Returns a stream of `HttpEvent<DocumentUploadResponse>` so the caller can
   * observe upload progress (UXR-505) and the final response.
   *
   * @param file      The `File` object selected or dropped by the user.
   * @param patientId GUID of the patient this document belongs to.
   */
  upload(
    file: File,
    patientId: string,
  ): Observable<HttpEvent<DocumentUploadResponse>> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('patientId', patientId);

    const req = new HttpRequest(
      'POST',
      '/api/v1/documents/upload',
      formData,
      {
        reportProgress: true,
      },
    );

    return this.http.request<DocumentUploadResponse>(req);
  }

  /**
   * Returns the current scan and processing status for a previously uploaded document.
   * The component polls this endpoint until scan and extraction states are both terminal.
   *
   * @param documentId  The `documentId` returned from the upload response.
   */
  getStatus(documentId: string): Observable<DocumentStatusResponse> {
    return this.http.get<DocumentStatusResponse>(
      `/api/v1/documents/${encodeURIComponent(documentId)}/status`,
    );
  }

  /**
   * Triggers a manual re-queue of OCR processing for a failed document (US_041 AC-4).
   * Up to 3 retries are allowed by the server before the job is moved to the
   * dead-letter queue.
   *
   * @param documentId  The `documentId` of the failed document.
   */
  retryOcr(documentId: string): Observable<void> {
    return this.http.post<void>(
      `/api/v1/documents/${encodeURIComponent(documentId)}/retry-ocr`,
      null,
    );
  }
}
