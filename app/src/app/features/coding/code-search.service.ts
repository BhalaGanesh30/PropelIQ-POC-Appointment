import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import type {
  AddFavoriteRequestDto,
  CodeFavoriteDto,
  CodeSearchResponseDto,
} from '../../shared/models/code-search.dto';

/**
 * HTTP service for code search and favorites management (US_052 / SCR-018).
 *
 * Endpoints:
 *   GET    /api/v1/codes/search?q={q}&type={type}&includeDeprecated={bool}
 *   GET    /api/v1/users/me/code-favorites
 *   POST   /api/v1/users/me/code-favorites
 *   DELETE /api/v1/users/me/code-favorites/{codeType}/{code}
 */
@Injectable({ providedIn: 'root' })
export class CodeSearchService {
  private readonly http = inject(HttpClient);

  search(
    q: string,
    type: 'all' | 'icd10' | 'cpt',
    includeDeprecated: boolean,
  ): Observable<CodeSearchResponseDto> {
    return this.http.get<CodeSearchResponseDto>('/api/v1/codes/search', {
      params: { q, type, includeDeprecated: String(includeDeprecated) },
    });
  }

  getFavorites(): Observable<CodeFavoriteDto[]> {
    return this.http.get<CodeFavoriteDto[]>('/api/v1/users/me/code-favorites');
  }

  addFavorite(req: AddFavoriteRequestDto): Observable<void> {
    return this.http.post<void>('/api/v1/users/me/code-favorites', req);
  }

  removeFavorite(codeType: string, code: string): Observable<void> {
    return this.http.delete<void>(
      `/api/v1/users/me/code-favorites/${codeType}/${encodeURIComponent(code)}`,
    );
  }
}
