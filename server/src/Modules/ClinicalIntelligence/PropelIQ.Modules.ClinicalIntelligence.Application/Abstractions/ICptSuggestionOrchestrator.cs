using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Orchestrates the full CPT / E/M suggestion Hybrid pipeline (US_050, FR-MC-002).
///
/// Always resolves to a <see cref="CptSuggestionResponseDto"/> (never throws for domain-level
/// edge cases — those are surfaced as response flags):
/// - Unmappable appointment type → <c>NoSuggestionForAppointmentType = true</c>.
/// - Circuit breaker open or all codes deprecated → empty result, <c>LowConfidence = true</c>.
/// </summary>
public interface ICptSuggestionOrchestrator
{
    /// <summary>
    /// Runs the CPT/E/M suggestion pipeline for the given patient and appointment.
    /// </summary>
    /// <param name="patientId">Target patient's GUID.</param>
    /// <param name="appointmentId">The appointment for which to generate CPT/E/M suggestions.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CptSuggestionResponseDto> GenerateCptSuggestionsAsync(
        Guid patientId,
        Guid appointmentId,
        CancellationToken ct = default);
}
