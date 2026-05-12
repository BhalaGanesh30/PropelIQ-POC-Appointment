namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Append-only log of significant events that occur on behalf of or for a specific user
/// (US_061, AC-2, AC-3).
///
/// <para>
/// Rows are written by application code at mutation boundaries (login, role change,
/// status change, bulk action) and queried in reverse-chronological order for the
/// per-user activity history panel.
/// </para>
///
/// <para>Table: <c>app.user_activity_logs</c></para>
/// <para>
/// The table does <em>not</em> inherit <c>BaseEntity</c> because activity log rows are
/// immutable after creation — no <c>updated_at</c> is needed.
/// </para>
/// </summary>
public sealed class UserActivityLog
{
    /// <summary>Unique record identifier (PK, generated server-side).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Domain user ID this event belongs to (FK → <c>app.users.id</c>, CASCADE delete).
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Machine-readable event category (max 50 chars).
    /// Examples: <c>"Login"</c>, <c>"RoleAssigned"</c>, <c>"UserActivated"</c>,
    /// <c>"UserDeactivated"</c>.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>Human-readable description of the event (AC-3).</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>UTC wall-clock time the event occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// User ID of the admin or system actor who triggered the event, if applicable.
    /// Nullable; SET NULL when the performing admin account is deleted.
    /// FK → <c>app.users.id</c>.
    /// </summary>
    public Guid? PerformedByUserId { get; init; }
}
