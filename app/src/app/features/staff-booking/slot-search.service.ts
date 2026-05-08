import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { SlotResult, ConflictCheck } from '../../shared/models/slot-result.model';

/**
 * Slot availability and conflict-check service for EP-004 US_035 (SCR-027).
 *
 * GET /api/v1/appointments/slots   — returns available slots for a given date/duration.
 * GET /api/v1/appointments/conflict-check — checks whether a patient already has
 *   a booking that overlaps the requested slot.
 */
@Injectable({ providedIn: 'root' })
export class SlotSearchService {
  private readonly http = inject(HttpClient);

  /**
   * Returns available (and unavailable) slots for the given filters.
   *
   * @param date      ISO-8601 date string (YYYY-MM-DD).
   * @param duration  Appointment duration in minutes (e.g. 15, 30, 60).
   * @param type      Optional appointment type filter.
   */
  searchSlots(date: string, duration: number, type?: string): Observable<SlotResult[]> {
    let params = new HttpParams().set('date', date).set('duration', String(duration));
    if (type) {
      params = params.set('type', type);
    }
    return this.http.get<SlotResult[]>('/api/v1/appointments/slots', { params });
  }

  /**
   * Checks whether the given patient already has a booking that conflicts with
   * the requested slot.
   *
   * @param patientId UUID of the patient.
   * @param slotId    UUID of the target slot.
   */
  checkConflict(patientId: string, slotId: string): Observable<ConflictCheck> {
    const params = new HttpParams().set('patientId', patientId).set('slotId', slotId);
    return this.http.get<ConflictCheck>('/api/v1/appointments/conflict-check', { params });
  }
}
