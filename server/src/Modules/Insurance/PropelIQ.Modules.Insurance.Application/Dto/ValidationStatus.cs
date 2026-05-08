using System.Text.Json.Serialization;

namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Possible outcomes of the insurance soft validation engine (EP-005 US_037).
///
/// Values are serialised as strings (matching the Angular FE union type
/// <c>InsuranceValidationStatus</c>) so that adding a new member does not
/// break existing API consumers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationStatus
{
    /// <summary>Policy number and provider code both passed all checks (AC-3).</summary>
    SoftValidated,

    /// <summary>
    /// Soft warning: provider found but policy number format does not match the
    /// expected pattern, or a secondary/primary duplicate was detected.
    /// Booking is NOT blocked (AC-2).
    /// </summary>
    Warning,

    /// <summary>
    /// Hard failure: provider code unknown AND format invalid.
    /// Record is flagged for staff review (AC-4).
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// Reference database was unreachable.
    /// Validation skipped; booking proceeds; a background retry is queued (Edge Case 1).
    /// </summary>
    ValidationPending,
}
