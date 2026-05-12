namespace PropelIQ.Modules.SharedServices.Application.Configuration;

/// <summary>
/// Represents a single entry in a configuration category's version history (US_059, AC-3).
/// Returned by <c>GET /api/v1/admin/config/{category}/history</c>.
/// </summary>
public sealed record ConfigurationVersionDto
{
    /// <summary>Unique identifier of the version row.</summary>
    public required Guid VersionId { get; init; }

    /// <summary>Monotonically increasing version number scoped to the category.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>The configuration category this version belongs to.</summary>
    public required ConfigurationCategory Category { get; init; }

    /// <summary>UTC timestamp when this version was persisted.</summary>
    public required DateTime ChangedAtUtc { get; init; }

    /// <summary>UUID of the admin who created this version.</summary>
    public required Guid ChangedByAdminId { get; init; }

    /// <summary>Display name of the admin for history display (AC-3).</summary>
    public required string ChangedByName { get; init; }

    /// <summary>Configuration values active in this version.</summary>
    public required Dictionary<string, object> Values { get; init; }

    /// <summary>Configuration values from the previous version. Null for version 1 (no prior state).</summary>
    public Dictionary<string, object>? PreviousValues { get; init; }

    /// <summary>When populated, this version was created by restoring the referenced source version (AC-4).</summary>
    public Guid? RestoredFromVersionId { get; init; }
}
