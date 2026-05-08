namespace PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

/// <summary>
/// Response body for POST /api/v1/walkins/{id}/convert (EP-004 US_033 AC-2).
/// </summary>
public sealed class ConvertWalkinResponse
{
    /// <summary>PK of the newly created Patient record.</summary>
    public required Guid PatientId { get; init; }

    /// <summary>PK of the WalkIn record that was converted.</summary>
    public required Guid WalkinId { get; init; }

    /// <summary>Human-readable conversion outcome (e.g., "Converted").</summary>
    public required string ConversionStatus { get; init; }
}
