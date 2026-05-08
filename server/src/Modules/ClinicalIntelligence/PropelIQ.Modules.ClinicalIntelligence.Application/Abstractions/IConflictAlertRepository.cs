using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository abstraction for <see cref="ConflictAlert"/> persistence.
/// </summary>
public interface IConflictAlertRepository
{
    /// <summary>Returns all conflict alerts for a given patient.</summary>
    Task<IReadOnlyList<ConflictAlert>> GetByPatientIdAsync(
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>Returns a single conflict alert by its ID, or null when not found.</summary>
    Task<ConflictAlert?> GetByIdAsync(Guid conflictId, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new alert only if no row with the same
    /// <c>(PatientId, FactIdA, FactIdB)</c> already exists (idempotent upsert).
    /// Returns the persisted or existing <see cref="ConflictAlert"/>.
    /// </summary>
    Task<ConflictAlert> UpsertAsync(
        ConflictAlert alert,
        CancellationToken ct = default);

    /// <summary>
    /// Sets <c>Acknowledged = true</c>, <c>AcknowledgedBy</c>, and <c>AcknowledgedAt</c>
    /// for the given alert row. Returns false when the conflict ID does not exist.
    /// </summary>
    Task<bool> AcknowledgeAsync(
        Guid conflictId,
        Guid clinicianId,
        DateTimeOffset acknowledgedAt,
        CancellationToken ct = default);
}
