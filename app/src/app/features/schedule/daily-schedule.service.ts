import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';

import { ScheduleAppointment } from '../../shared/models/schedule-appointment.model';
import { RescheduleRequest } from '../../shared/models/reschedule-request.model';

/**
 * HTTP service for SCR-026 Daily Schedule (FR-SO-006).
 *
 * getSchedule — GET /api/v1/schedule/daily?date=yyyy-MM-dd
 * reschedule  — PUT /api/v1/schedule/reschedule  (drag-drop, AC-2)
 */
@Injectable({ providedIn: 'root' })
export class DailyScheduleService {
  private readonly http = inject(HttpClient);

  /**
   * Loads all appointments for the given date.
   *
   * @param date  ISO date string in yyyy-MM-dd format (e.g. "2026-05-06").
   * @returns Observable of appointment blocks sorted by startTime (AC-1).
   */
  getSchedule(date: string): Observable<ScheduleAppointment[]> {
    const params = new HttpParams().set('date', date);
    return this.http
      .get<ScheduleAppointment[]>('/api/v1/schedule/daily', { params })
      .pipe(
        // Backward-compatible fallback while older API route templates may still be running.
        catchError((err: { status?: number }) => {
          if (err?.status !== 404) return throwError(() => err);
          return this.http.get<ScheduleAppointment[]>(
            '/api/v1/schedule/api/v1/schedule/daily',
            { params },
          );
        }),
      );
  }

  /**
   * Persists a drag-drop reschedule with a mandatory override reason (AC-2).
   *
   * @param payload RescheduleRequest with new start time and override reason.
   */
  reschedule(payload: RescheduleRequest): Observable<void> {
    return this.http
      .put<void>('/api/v1/schedule/reschedule', payload)
      .pipe(
        catchError((err: { status?: number }) => {
          if (err?.status !== 404) return throwError(() => err);
          return this.http.put<void>(
            '/api/v1/schedule/api/v1/schedule/reschedule',
            payload,
          );
        }),
      );
  }
}
