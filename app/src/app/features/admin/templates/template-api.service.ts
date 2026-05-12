import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  TemplateListItem,
  TemplateDetail,
  TemplateVersionItem,
  TemplatePagedResult,
  SaveTemplateRequest,
  PreviewResponse,
  TemplateValidationResult,
} from './models/template.models';

/**
 * HTTP client service for the SCR-024 Template Editor (US_062, AC-1–AC-4).
 *
 * Wraps all endpoints exposed by `TemplatesController`
 * at `/api/v1/admin/templates`.
 */
@Injectable({ providedIn: 'root' })
export class TemplateApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/templates';

  /**
   * Returns a paginated list of templates, optionally filtered by type.
   * @param type 'HTML' | 'SMS' — omit for all templates.
   */
  list(
    type?: string,
    page = 1,
    pageSize = 25,
  ): Observable<TemplatePagedResult<TemplateListItem>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (type) {
      params = params.set('typeFilter', type);
    }
    return this.http.get<TemplatePagedResult<TemplateListItem>>(this.base, { params });
  }

  /** Returns full template detail including the current active version. */
  getById(templateId: string): Observable<TemplateDetail> {
    return this.http.get<TemplateDetail>(`${this.base}/${templateId}`);
  }

  /** Returns paginated version history for a template (newest first). */
  getVersions(
    templateId: string,
    page = 1,
    pageSize = 50,
  ): Observable<TemplateVersionItem[]> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http.get<TemplateVersionItem[]>(
      `${this.base}/${templateId}/versions`,
      { params },
    );
  }

  /**
   * Validates content and saves it as a new immutable version (AC-1).
   * Returns 422 when content contains invalid merge-field placeholders (AC-4).
   */
  save(
    templateId: string,
    request: SaveTemplateRequest,
  ): Observable<TemplateVersionItem> {
    return this.http.post<TemplateVersionItem>(`${this.base}/${templateId}`, request);
  }

  /**
   * Renders draft content with sample merge-field values (AC-2).
   * No DB write occurs.
   */
  preview(
    templateId: string,
    content: string,
    subject?: string,
  ): Observable<PreviewResponse> {
    return this.http.post<PreviewResponse>(`${this.base}/${templateId}/preview`, {
      content,
      subject: subject ?? null,
    });
  }

  /**
   * Copies old version content as new active version without touching other rows (AC-3).
   */
  restore(
    templateId: string,
    versionId: string,
  ): Observable<TemplateVersionItem> {
    return this.http.post<TemplateVersionItem>(
      `${this.base}/${templateId}/restore/${versionId}`,
      {},
    );
  }

  /**
   * Validates merge-field placeholders in raw content (AC-4, edge case 2).
   * The backend endpoint accepts a plain JSON string body.
   */
  validate(
    templateId: string,
    content: string,
  ): Observable<TemplateValidationResult> {
    return this.http.post<TemplateValidationResult>(
      `${this.base}/${templateId}/validate`,
      JSON.stringify(content),
      { headers: { 'Content-Type': 'application/json' } },
    );
  }
}
