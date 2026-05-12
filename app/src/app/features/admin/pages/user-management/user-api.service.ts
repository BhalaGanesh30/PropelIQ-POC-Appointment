import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import {
  UserListItem,
  UserDetail,
  PagedResult,
  BulkActionRequest,
  BulkActionTypeName,
  BulkActionResult,
  UserActivityEntry,
} from './models/user.models';

/** Wire shape sent to POST /api/v1/admin/users/bulk — action as integer enum. */
interface BulkActionRequestWire {
  userIds: string[];
  action: number;
  targetRole?: string;
}

/** Maps display name to backend BulkActionType integer values. */
const ACTION_INDEX: Record<BulkActionTypeName, number> = {
  Activate: 0,
  Deactivate: 1,
  AssignRole: 2,
};

@Injectable({ providedIn: 'root' })
export class UserApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin/users';

  list(
    searchTerm?: string,
    roleFilter?: string,
    statusFilter?: string,
    page = 1,
    pageSize = 25,
  ): Observable<PagedResult<UserListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (searchTerm) params = params.set('searchTerm', searchTerm);
    if (roleFilter) params = params.set('roleFilter', roleFilter);
    if (statusFilter) params = params.set('statusFilter', statusFilter);
    return this.http.get<PagedResult<UserListItem>>(this.base, { params });
  }

  getById(userId: string): Observable<UserDetail> {
    return this.http.get<UserDetail>(`${this.base}/${userId}`);
  }

  bulkAction(request: BulkActionRequest): Observable<BulkActionResult> {
    const wire: BulkActionRequestWire = {
      userIds: request.userIds,
      action: ACTION_INDEX[request.action],
      targetRole: request.targetRole,
    };
    return this.http.post<BulkActionResult>(`${this.base}/bulk`, wire);
  }

  getActivityHistory(
    userId: string,
    page = 1,
    pageSize = 25,
  ): Observable<UserActivityEntry[]> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http.get<PagedResult<UserActivityEntry>>(
      `${this.base}/${userId}/activity`,
      { params },
    ).pipe(map((r) => r.items));
  }
}
