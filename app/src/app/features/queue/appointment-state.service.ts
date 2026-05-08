import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { QueueEntry } from './models/queue-entry.model';

/** Actions sent to PATCH /api/v1/appointments/{id}/state (US_032 AC-1 – AC-4). */
export type CheckinAction = 'check-in' | 'start-visit' | 'complete-visit' | 'no-show';

/** Request body for the state-transition endpoint. */
interface StateTransitionBody {
  action: CheckinAction;
}

/**
 * Thin HTTP client wrapping PATCH /api/v1/appointments/{id}/state.
 *
 * Each call returns the updated QueueEntry so the caller can update the queue
 * signal in-place without requiring a full list refresh.
 *
 * Error handling is left to the caller (CheckinActionsComponent) so the
 * appropriate user-facing message (snackbar) can be shown in context.
 */
@Injectable({ providedIn: 'root' })
export class AppointmentStateService {
  private readonly http = inject(HttpClient);

  /**
   * Sends a state-transition request for the given appointment.
   *
   * @param appointmentId - UUID of the appointment to transition.
   * @param action        - Transition verb (AC-1 through AC-4).
   * @returns Observable emitting the server-updated QueueEntry on success.
   */
  transitionState(
    appointmentId: string,
    action: CheckinAction,
  ): Observable<QueueEntry> {
    const body: StateTransitionBody = { action };
    return this.http.patch<QueueEntry>(
      `/api/v1/appointments/${appointmentId}/state`,
      body,
    );
  }
}
