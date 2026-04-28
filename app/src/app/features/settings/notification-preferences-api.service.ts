import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  NotificationPreferenceDto,
  NotificationPreferenceResponse,
} from './models/notification-preference.models';

/**
 * HTTP client for the patient notification preferences API (US_029 task_001).
 *
 * GET  /api/v1/notification-preferences — returns current channel and timing prefs.
 * PUT  /api/v1/notification-preferences — saves updated prefs and returns new state.
 *
 * Authentication is attached by the HTTP auth interceptor — no manual header
 * manipulation required.  The backend enforces the PatientOnly role, so this
 * service must only be invoked from patient-facing screens.
 */
@Injectable({ providedIn: 'root' })
export class NotificationPreferencesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/notification-preferences';

  /** Retrieve the authenticated patient's current notification preferences. */
  getPreferences(): Observable<NotificationPreferenceResponse> {
    return this.http.get<NotificationPreferenceResponse>(this.baseUrl);
  }

  /**
   * Persist updated notification preferences.
   * Returns the saved state (including `hasPhoneNumber`) for confirmation.
   */
  savePreferences(dto: NotificationPreferenceDto): Observable<NotificationPreferenceResponse> {
    return this.http.put<NotificationPreferenceResponse>(this.baseUrl, dto);
  }
}
