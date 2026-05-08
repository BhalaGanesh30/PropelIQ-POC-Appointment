import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { DocumentCategory } from '../../shared/models/document-category.enum';
import type { DocumentListItem } from '../../shared/models/document-list-item.model';
import type { DocumentListFilter } from '../../shared/models/document-list-filter.model';

/** Generic paginated envelope returned by the documents list endpoint. */
export interface DocumentPagedResponse {
  items: DocumentListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/**
 * Angular service wrapping the document library API endpoints (EP-006 US_043).
 *
 * Endpoints consumed:
 *   GET    /api/v1/documents                — list (paginated, filtered)
 *   PATCH  /api/v1/documents/{id}/category  — categorize (AC-1)
 *   PATCH  /api/v1/documents/{id}/rename    — rename     (AC-2)
 *   DELETE /api/v1/documents/{id}           — soft-delete (AC-3)
 *   POST   /api/v1/documents/{id}/restore   — restore     (AC-4)
 */
@Injectable({ providedIn: 'root' })
export class DocumentLibraryService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/documents';

  /**
   * Returns a paginated, optionally filtered list of documents.
   * When `filter.includeDeleted` is true, soft-deleted documents are included
   * (admin trash view — AC-4).
   */
  listDocuments(
    patientId: string,
    filter: DocumentListFilter,
    page: number,
    pageSize: number,
  ): Observable<DocumentPagedResponse> {
    let params = new HttpParams()
      .set('patientId', patientId)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString())
      .set('includeDeleted', filter.includeDeleted.toString());

    if (filter.category)  params = params.set('category', filter.category);
    if (filter.dateFrom)  params = params.set('dateFrom', filter.dateFrom);
    if (filter.dateTo)    params = params.set('dateTo', filter.dateTo);
    if (filter.status)    params = params.set('status', filter.status);

    return this.http.get<DocumentPagedResponse>(this.base, { params });
  }

  /**
   * Assigns a category to a document (AC-1).
   * Safe to call when `extractionStatus` is not yet Completed (Edge Case 1 — OCR
   * completion never overwrites the category).
   */
  categorize(documentId: string, category: DocumentCategory): Observable<void> {
    return this.http.patch<void>(
      `${this.base}/${documentId}/category`,
      { category },
    );
  }

  /**
   * Updates the display name of a document (AC-2).
   * The original storage filename (R2 object key) is never modified.
   */
  rename(documentId: string, displayName: string): Observable<void> {
    return this.http.patch<void>(
      `${this.base}/${documentId}/rename`,
      { displayName },
    );
  }

  /**
   * Soft-deletes a document — sets `is_deleted = true` and records `deleted_at` (AC-3).
   * No hard-delete endpoint exists in this service (Edge Case 2).
   */
  softDelete(documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${documentId}`);
  }

  /**
   * Restores a soft-deleted document (AC-4 — admin trash view).
   * Clears `is_deleted` and `deleted_at` on the server.
   */
  restore(documentId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${documentId}/restore`, {});
  }
}
