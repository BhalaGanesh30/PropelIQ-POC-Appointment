import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface InviteStaffRequest {
  fullName: string;
  email: string;
  role: string;
}

export interface InviteStaffResponse {
  userId: string;
  email: string;
  accountStatus: string;
  invitationExpiresAt: string;
}

export interface ActivateStaffRequest {
  token: string;
  email: string;
  password: string;
}

export interface StaffListItem {
  id: string;
  fullName: string;
  email: string;
  role: string;
  accountStatus: string;
  invitedAt: string | null;
  activatedAt: string | null;
  deactivatedAt: string | null;
}

export interface StaffListResponse {
  items: StaffListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class StaffManagementService {
  private readonly http = inject(HttpClient);
  /** Matches the backend route: /api/v1/staffmanagement */
  private readonly apiUrl = `${environment.apiBaseUrl}/staffmanagement`;

  getStaffList(
    page = 1,
    pageSize = 25,
    status?: string,
    search?: string,
  ): Observable<StaffListResponse> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    if (search) params = params.set('search', search);
    return this.http.get<StaffListResponse>(this.apiUrl, { params });
  }

  inviteStaff(request: InviteStaffRequest): Observable<InviteStaffResponse> {
    return this.http.post<InviteStaffResponse>(`${this.apiUrl}/invite`, request);
  }

  deactivateStaff(userId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/${userId}/deactivate`,
      {},
    );
  }

  activateStaff(request: ActivateStaffRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/activate`, request);
  }
}
