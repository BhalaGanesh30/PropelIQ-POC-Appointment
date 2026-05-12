namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Deterministic appointment-type-to-CPT-candidacy lookup (US_050, Edge Case 1).
///
/// Reads the configured list of mappable appointment types from
/// <c>IConfiguration["CPT:MappableAppointmentTypes"]</c>.
/// </summary>
public interface IAppointmentTypeMapper
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="appointmentType"/> is in the configured set
    /// of types that can yield CPT suggestions.
    /// </summary>
    bool IsMappableToCpt(string appointmentType);
}
