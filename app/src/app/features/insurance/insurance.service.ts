import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  InsuranceFormData,
  InsuranceProfile,
} from '../../shared/models/insurance-form-data.model';
import { InsuranceValidationResult } from '../../shared/models/insurance-validation-result.model';

/**
 * HTTP service for EP-005 US_037 insurance soft validation (FR-IP-001).
 *
 * validate — POST /api/v1/insurance/validate  (AC-1)
 * save     — POST /api/v1/insurance           (AC-3 / AC-4 persist with status flag)
 */
@Injectable({ providedIn: 'root' })
export class InsuranceService {
  private readonly http = inject(HttpClient);

  /**
   * Soft-validates insurance details against the reference database.
   * Completes within 500 ms per AC-1 / NFR-002.
   *
   * The result is advisory only — callers MUST NOT block booking regardless
   * of the returned status (AC-2).
   *
   * @param data  Insurance form payload for a single tier.
   */
  validate(data: InsuranceFormData): Observable<InsuranceValidationResult> {
    // Send JSON-only payload (no file upload at validation time — images
    // are uploaded separately via the save() multipart call).
    const body = {
      tier:         data.tier,
      policyNumber: data.policyNumber,
      providerCode: data.providerCode,
      providerName: data.providerName,
      groupNumber:  data.groupNumber,
    };
    return this.http.post<InsuranceValidationResult>(
      '/api/v1/insurance/validate',
      body,
    );
  }

  /**
   * Persists the insurance record with the validation status flag.
   * Sends a multipart/form-data body when card images are attached (UXR-505).
   *
   * @param data Insurance form payload including optional card image Files.
   */
  save(data: InsuranceFormData): Observable<InsuranceProfile> {
    const form = new FormData();
    form.append('tier',         data.tier);
    form.append('policyNumber', data.policyNumber);
    form.append('providerCode', data.providerCode);
    form.append('providerName', data.providerName);
    if (data.groupNumber) form.append('groupNumber', data.groupNumber);
    if (data.cardImageFront)
      form.append('cardImageFront', data.cardImageFront, data.cardImageFront.name);
    if (data.cardImageBack)
      form.append('cardImageBack',  data.cardImageBack,  data.cardImageBack.name);

    return this.http.post<InsuranceProfile>('/api/v1/insurance', form);
  }
}
