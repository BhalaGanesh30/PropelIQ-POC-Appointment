import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs/operators';

import { PatientSearchResult } from '../../shared/models/patient-search-result.model';

/**
 * Debounced HTTP client for patient search (EP-004 US_033 AC-4).
 * GET /api/v1/patients/search?q={query} — returns up to 10 matching patients.
 *
 * Debounce: 300 ms; minimum query length: 2 characters.
 * switchMap cancels previous in-flight requests when a new query arrives.
 */
@Injectable({ providedIn: 'root' })
export class PatientSearchService {
  private readonly http = inject(HttpClient);

  /**
   * Returns a debounced search results stream for the given query subject.
   *
   * Usage in a component:
   * ```ts
   * readonly searchQuery$ = new Subject<string>();
   * readonly results$ = this.patientSearch.results$(this.searchQuery$);
   * // then: this.searchQuery$.next(inputValue);
   * ```
   */
  results$(query$: Subject<string>): Observable<PatientSearchResult[]> {
    return query$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      filter((q) => q.length >= 2),
      switchMap((q) =>
        this.http.get<PatientSearchResult[]>('/api/v1/patients/search', {
          params: { q },
        }),
      ),
    );
  }

  /** Direct single-call search — used when pre-populating from a known query. */
  search(query: string): Observable<PatientSearchResult[]> {
    return this.http.get<PatientSearchResult[]>('/api/v1/patients/search', {
      params: { q: query },
    });
  }
}
