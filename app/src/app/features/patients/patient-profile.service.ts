import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import type { PatientProfileDto, PatientHeaderDto } from '../../shared/models/clinical-fact.model';

/**
 * Angular service wrapping the patient profile API (EP-007 US_045).
 *
 * Endpoints consumed:
 *   GET /api/v1/patients/{id}/profile   — facts + partialSources for the active tab
 *   GET /api/v1/patients/{id}           — patient header (name, DOB, MRN)
 */
@Injectable({ providedIn: 'root' })
export class PatientProfileService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/patients';

  /**
   * Fetches clinical facts for the given patient, scoped to a fact-type tab.
   * Pass `factType` to load only that category (section-based lazy loading — AC-1).
   * Omit `factType` (or pass 'summary') to receive all fact types at once.
   */
  getProfile(patientId: string, factType?: string): Observable<PatientProfileDto> {
    const url = `${this.base}/${patientId}/profile`;
    if (factType && factType !== 'summary') {
      return this.http.get<PatientProfileDto>(url, { params: { factType } });
    }
    return this.http.get<PatientProfileDto>(url);
  }

  /** Fetches the patient header (name, DOB, MRN) for the profile page header. */
  getPatientHeader(patientId: string): Observable<PatientHeaderDto> {
    return this.http.get<PatientHeaderDto>(`${this.base}/${patientId}`);
  }
}
