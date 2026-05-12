namespace PropelIQ.Modules.SharedServices.Application.Configuration;

/// <summary>
/// Payload for configuration update requests submitted to <c>PUT /api/v1/admin/config/{category}</c>
/// (US_059, AC-1).
///
/// <para>
/// <see cref="ExpectedVersion"/> and <see cref="AdminId"/> are populated server-side from the
/// <c>If-Match</c> header and the JWT sub claim respectively — clients send only <see cref="Values"/>.
/// </para>
/// </summary>
public sealed record ConfigurationUpdateRequest
{
    /// <summary>Key-value configuration entries to persist for the target category.</summary>
    public required Dictionary<string, object> Values { get; init; }

    /// <summary>
    /// Version number the client currently holds. Compared against the DB's current version
    /// for optimistic concurrency control (edge case 1). Set from the <c>If-Match</c> header.
    /// </summary>
    public required int ExpectedVersion { get; init; }

    /// <summary>UUID of the authenticated admin performing the update. Set from the JWT sub claim.</summary>
    public required Guid AdminId { get; init; }

    /// <summary>Display name of the admin. Set from the JWT name/email claim for history display (AC-3).</summary>
    public string AdminName { get; init; } = "Unknown";
}
