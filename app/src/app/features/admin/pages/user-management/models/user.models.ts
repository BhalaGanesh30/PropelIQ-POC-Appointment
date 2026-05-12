/** Matches backend UserListItem DTO (camelCase JSON from /api/v1/admin/users). */
export interface UserListItem {
  userId: string;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  lastLoginAt: string | null;
}

/** Matches backend UserDetailDto (camelCase JSON from /api/v1/admin/users/{id}). */
export interface UserDetail {
  userId: string;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  lastLoginAt: string | null;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/**
 * Display names for bulk action types.
 * Mapped to backend BulkActionType enum integers (0/1/2) in UserApiService.
 */
export type BulkActionTypeName = 'Activate' | 'Deactivate' | 'AssignRole';

export interface BulkActionRequest {
  userIds: string[];
  action: BulkActionTypeName;
  targetRole?: string;
}

export interface BulkActionFailure {
  userId: string;
  userName: string;
  reason: string;
}

export interface BulkActionResult {
  successCount: number;
  failureCount: number;
  failures: BulkActionFailure[];
}

/** Matches backend UserActivityEntry DTO (camelCase JSON from /api/v1/admin/users/{id}/activity). */
export interface UserActivityEntry {
  id: string;
  eventType: string;
  description: string;
  occurredAt: string;
  performedByUserId: string | null;
}
