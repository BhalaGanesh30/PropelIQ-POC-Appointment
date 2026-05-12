import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ConfigCategory,
  ConfigSnapshot,
  ConfigVersion,
  ConfigUpdateResult,
} from './models/config.models';

/**
 * HTTP client for all configuration endpoints (US_059, AC-1–AC-4).
 *
 * Base URL: /api/v1/admin/config
 *
 * GET  /{category}               — full response so the ETag header is accessible.
 * PUT  /{category}               — requires If-Match header for OCC (edge case 1).
 * GET  /{category}/history       — version history list (AC-3).
 * POST /{category}/restore/{id}  — rollback (AC-4).
 */
@Injectable({ providedIn: 'root' })
export class ConfigApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/config';

  /**
   * Fetches the current configuration snapshot with the full HTTP response so
   * the caller can read the ETag header for optimistic concurrency (edge case 1).
   */
  getCurrent(category: ConfigCategory): Observable<HttpResponse<ConfigSnapshot>> {
    return this.http.get<ConfigSnapshot>(`${this.base}/${category}`, {
      observe: 'response',
    });
  }

  /**
   * Submits updated configuration values.
   * @param etag The raw version number from the ETag header (without quotes).
   */
  update(
    category: ConfigCategory,
    values: Record<string, unknown>,
    etag: string,
  ): Observable<ConfigUpdateResult> {
    return this.http.put<ConfigUpdateResult>(
      `${this.base}/${category}`,
      { values },
      {
        headers: new HttpHeaders({ 'If-Match': `"${etag}"` }),
      },
    );
  }

  /** Returns the full ordered version history for the given category (AC-3). */
  getHistory(category: ConfigCategory): Observable<ConfigVersion[]> {
    return this.http.get<ConfigVersion[]>(`${this.base}/${category}/history`);
  }

  /** Creates a new version by restoring the snapshot of a historical version (AC-4). */
  restore(
    category: ConfigCategory,
    versionId: string,
  ): Observable<ConfigUpdateResult> {
    return this.http.post<ConfigUpdateResult>(
      `${this.base}/${category}/restore/${versionId}`,
      {},
    );
  }
}
