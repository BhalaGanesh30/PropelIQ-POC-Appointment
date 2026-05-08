import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { StaffBookingRequest } from '../../shared/models/staff-booking-request.model';
import { StaffBookingResponse } from '../../shared/models/staff-booking-response.model';

/**
 * HTTP service for staff-assisted booking (EP-004 US_035 AC-1, AC-2).
 *
 * POST /api/v1/staff-bookings — creates an appointment on behalf of a patient.
 * The acting staff user identity is determined server-side from the JWT claim
 * and stored in the audit log (AC-2).
 */
@Injectable({ providedIn: 'root' })
export class StaffBookingService {
  private readonly http = inject(HttpClient);

  /**
   * Creates an appointment on behalf of a patient.
   *
   * @param payload  Booking request containing patientId (or newPatient inline
   *                 form), slotId, visitReason, and optional overrideReason.
   */
  createBooking(payload: StaffBookingRequest): Observable<StaffBookingResponse> {
    return this.http.post<StaffBookingResponse>('/api/v1/staff-bookings', payload);
  }
}
