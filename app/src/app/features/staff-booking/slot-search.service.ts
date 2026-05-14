import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { SlotResult, ConflictCheck } from '../../shared/models/slot-result.model';

/** Interim type for backend API response structure. */
interface BackendSlotGroup {
  date: string;
  slots: Array<{
    id: string;
    startTime: string;
    endTime: string;
    durationMinutes: number;
    type: string;
    providerName?: string;
    location?: string;
    availableCapacity: number;
  }>;
}

interface BackendSlotResponse {
  days: BackendSlotGroup[];
  totalAvailableSlots: number;
  hasResults: boolean;
}

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
    // Parse date and create a date range for that full day (start of day to end of day).
    const parsedDate = new Date(date + 'T00:00:00Z');
    const dateFrom = parsedDate.toISOString();
    const dateToDate = new Date(parsedDate);
    dateToDate.setUTCHours(23, 59, 59, 999);
    const dateTo = dateToDate.toISOString();

    let params = new HttpParams()
      .set('dateFrom', dateFrom)
      .set('dateTo', dateTo)
      .set('duration', String(duration));
    if (type) {
      params = params.set('type', type);
    }
    return this.http.get<BackendSlotResponse>('/api/v1/appointments/slots', { params })
      .pipe(
        map((response) => {
          // Flatten the grouped response into a simple array of slots.
          const flattened: SlotResult[] = [];
          response.days.forEach((dayGroup) => {
            dayGroup.slots.forEach((slot) => {
              flattened.push({
                slotId: slot.id,
                dateTime: slot.startTime,
                duration: slot.durationMinutes,
                available: slot.availableCapacity > 0,
                providerName: slot.providerName,
              });
            });
          });
          return flattened;
        }),
      );
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
