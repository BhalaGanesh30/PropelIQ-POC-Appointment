import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AppointmentListItem,
  AppointmentFilter,
  CancelRequest,
  CancelResponse,
  PaginatedResponse,
  RescheduleRequest,
  RescheduleResponse,
} from './models/appointment.models';

/**
 * HTTP client for appointment cancel and reschedule endpoints.
 *
 * GET  /api/v1/bookings                    — getAppointments (paginated list)
 * POST /api/v1/bookings/{id}/cancel        — cancelAppointment (AC-1, AC-4)
 * POST /api/v1/bookings/{id}/reschedule    — rescheduleAppointment (AC-2, AC-4)
 */
@Injectable({ providedIn: 'root' })
export class AppointmentApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/bookings';

  /**
   * Retrieves a paginated, filtered list of appointments for the current user.
   * Relies on the HttpClient auth interceptor to attach the Bearer token.
   */
  getAppointments(
    filter: AppointmentFilter,
  ): Observable<PaginatedResponse<AppointmentListItem>> {
    let params = new HttpParams()
      .set('page', filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.startDate) params = params.set('startDate', filter.startDate);
    if (filter.endDate) params = params.set('endDate', filter.endDate);
    if (filter.status) params = params.set('status', filter.status);

    return this.http.get<PaginatedResponse<AppointmentListItem>>(this.baseUrl, {
      params,
    });
  }

  /**
   * Cancels a confirmed appointment (AC-1).
   * For staff overrides within 24 h, `overrideReason` is mandatory (AC-4).
   */
  cancelAppointment(
    id: string,
    request: CancelRequest,
  ): Observable<CancelResponse> {
    return this.http.post<CancelResponse>(
      `${this.baseUrl}/${encodeURIComponent(id)}/cancel`,
      request,
    );
  }

  /**
   * Reschedules a confirmed appointment to a new slot (AC-2).
   * For staff overrides within 24 h, `overrideReason` is mandatory (AC-4).
   */
  rescheduleAppointment(
    id: string,
    request: RescheduleRequest,
  ): Observable<RescheduleResponse> {
    return this.http.post<RescheduleResponse>(
      `${this.baseUrl}/${encodeURIComponent(id)}/reschedule`,
      request,
    );
  }
}
