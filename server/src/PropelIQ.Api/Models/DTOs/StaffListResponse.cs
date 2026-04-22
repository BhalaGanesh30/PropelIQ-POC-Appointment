namespace PropelIQ.Api.Models.DTOs;

/// <summary>
/// Paginated list of staff accounts returned by GET /api/v1/admin/staff.
/// </summary>
public sealed record StaffListResponse(
    List<StaffListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Summary projection of a single staff account.
/// </summary>
public sealed record StaffListItem(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string AccountStatus,
    DateTimeOffset? InvitedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? DeactivatedAt);
