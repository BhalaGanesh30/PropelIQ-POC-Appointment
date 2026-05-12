namespace PropelIQ.Modules.SharedServices.Application.Configuration;

/// <summary>
/// Service contract for versioned system configuration management (US_059, AC-1–AC-4).
///
/// <para>
/// Each call to <see cref="UpdateAsync"/> creates an immutable new <c>configuration_versions</c> row
/// (never an UPDATE). Optimistic concurrency is enforced via <see cref="ConfigurationUpdateRequest.ExpectedVersion"/>.
/// </para>
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Returns the current active configuration snapshot for the given category.
    /// Reads from in-memory cache when available; falls back to the database on cache miss.
    /// </summary>
    Task<ConfigurationSnapshot> GetCurrentAsync(
        ConfigurationCategory category,
        CancellationToken ct = default);

    /// <summary>
    /// Validates <paramref name="request"/> values and, if valid, persists a new version row.
    ///
    /// Returns <see cref="ConfigurationUpdateResult.ConflictDetected"/> when the
    /// <see cref="ConfigurationUpdateRequest.ExpectedVersion"/> does not match the database's
    /// current version (edge case 1 — concurrent admin edit).
    /// </summary>
    Task<ConfigurationUpdateResult> UpdateAsync(
        ConfigurationCategory category,
        ConfigurationUpdateRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full version history for <paramref name="category"/>, newest first (AC-3).
    /// </summary>
    Task<IReadOnlyList<ConfigurationVersionDto>> GetHistoryAsync(
        ConfigurationCategory category,
        CancellationToken ct = default);

    /// <summary>
    /// Re-applies a historical version as a new current version without overwriting history (AC-4).
    /// Validates the restored values against current business rules before persisting.
    /// </summary>
    Task<ConfigurationUpdateResult> RestoreVersionAsync(
        ConfigurationCategory category,
        Guid versionId,
        Guid adminId,
        CancellationToken ct = default);
}

/// <summary>Current active configuration snapshot returned by <see cref="IConfigurationService.GetCurrentAsync"/>.</summary>
public sealed record ConfigurationSnapshot
{
    public required Guid VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required ConfigurationCategory Category { get; init; }
    public required Dictionary<string, object> Values { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public required string UpdatedByName { get; init; }
}

/// <summary>
/// Result of a write operation (<see cref="IConfigurationService.UpdateAsync"/> or
/// <see cref="IConfigurationService.RestoreVersionAsync"/>).
/// </summary>
public sealed record ConfigurationUpdateResult
{
    public required bool Success { get; init; }
    public Guid? VersionId { get; init; }
    public int? VersionNumber { get; init; }
    public bool ConflictDetected { get; init; }

    /// <summary>Populated when <see cref="ConflictDetected"/> is true — the real current value.</summary>
    public ConfigurationSnapshot? CurrentValue { get; init; }

    /// <summary>Populated when validation fails — one message per violated constraint (AC-2).</summary>
    public IReadOnlyList<string>? ValidationErrors { get; init; }
}
