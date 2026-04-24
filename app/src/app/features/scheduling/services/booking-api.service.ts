import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ArtifactType,
  BookingResponse,
  CreateBookingRequest,
} from '../models/booking.model';

/**
 * HTTP client for the booking API.
 *
 * POST /api/v1/bookings        — createBooking (AC-1)
 * GET  /api/v1/bookings/{id}   — getBooking
 * GET  /api/v1/bookings/{id}/artifacts/{type} — downloadArtifact (AC-3)
 */
@Injectable({ providedIn: 'root' })
export class BookingApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/bookings';

  createBooking(request: CreateBookingRequest): Observable<BookingResponse> {
    return this.http.post<BookingResponse>(this.baseUrl, request);
  }

  getBooking(id: string): Observable<BookingResponse> {
    return this.http.get<BookingResponse>(`${this.baseUrl}/${id}`);
  }

  /** AC-3: returns a Blob so the caller can trigger a browser file download. */
  downloadArtifact(appointmentId: string, type: ArtifactType): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl}/${appointmentId}/artifacts/${type}`,
      { responseType: 'blob' },
    );
  }
}
