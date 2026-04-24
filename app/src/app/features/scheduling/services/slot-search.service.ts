import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SlotSearchParams, SlotSearchResponse } from '../models/slot.model';
import { environment } from '../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SlotSearchService {
  private readonly baseUrl = `${environment.apiBaseUrl}/appointments/slots`;

  private readonly http = inject(HttpClient);

  /**
   * Search available appointment slots within the given date range (AC-1).
   * Requires a valid JWT bearer token — the auth interceptor attaches it automatically.
   */
  searchSlots(params: SlotSearchParams): Observable<SlotSearchResponse> {
    let httpParams = new HttpParams()
      .set('dateFrom', params.dateFrom)
      .set('dateTo', params.dateTo);

    if (params.duration !== undefined) {
      httpParams = httpParams.set('duration', params.duration.toString());
    }
    if (params.type !== undefined) {
      httpParams = httpParams.set('type', params.type);
    }

    return this.http.get<SlotSearchResponse>(this.baseUrl, {
      params: httpParams,
    });
  }
}
