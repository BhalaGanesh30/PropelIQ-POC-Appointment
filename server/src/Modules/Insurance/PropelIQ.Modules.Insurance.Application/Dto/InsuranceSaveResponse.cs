namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Response returned by <c>POST /api/v1/insurance</c> after the profile is persisted
/// (EP-005 US_037 AC-3, AC-4).
/// </summary>
public sealed class InsuranceSaveResponse
{
    /// <summary>UUID of the newly created or updated <c>InsuranceProfile</c> record.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>UUID of the patient the profile belongs to.</summary>
    public required Guid PatientId { get; init; }

    /// <summary>Tier of the saved profile ("Primary" or "Secondary").</summary>
    public required string Tier { get; init; }

    /// <summary>
    /// Validation status that was persisted.  Reflects the status returned by the
    /// preceding validate call so the client can correlate without a second round-trip.
    /// </summary>
    public required string ValidationStatus { get; init; }
}
