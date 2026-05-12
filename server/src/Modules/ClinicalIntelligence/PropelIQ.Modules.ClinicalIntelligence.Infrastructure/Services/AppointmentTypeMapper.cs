using Microsoft.Extensions.Configuration;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Configuration-driven appointment-type-to-CPT-candidacy mapper (US_050, Edge Case 1).
///
/// Reads <c>CPT:MappableAppointmentTypes</c> from configuration — a comma-separated list
/// of appointment type strings, e.g. <c>"GENERAL,FOLLOWUP,NEW_PATIENT,ANNUAL"</c>.
///
/// Comparison is case-insensitive.  An empty or missing config value means no types
/// are mappable — all requests return <c>noSuggestionForAppointmentType: true</c>.
/// </summary>
internal sealed class AppointmentTypeMapper : IAppointmentTypeMapper
{
    private readonly HashSet<string> _mappableTypes;

    public AppointmentTypeMapper(IConfiguration configuration)
    {
        var raw = configuration["CPT:MappableAppointmentTypes"] ?? string.Empty;
        _mappableTypes = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool IsMappableToCpt(string appointmentType)
        => _mappableTypes.Contains(appointmentType);
}
