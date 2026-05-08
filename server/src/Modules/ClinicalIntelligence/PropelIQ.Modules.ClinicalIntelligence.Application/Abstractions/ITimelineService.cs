using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Aggregation service for the clinical timeline (US_048, FR-CA-005).
///
/// Merges events from <c>clinical_facts</c> and <c>clinical_documents</c>, applies
/// server-side category and date-range filters, and returns a reverse-chronological list.
/// Results are cached for 60 seconds per patient + filter combination (TR-004).
/// </summary>
public interface ITimelineService
{
    /// <summary>
    /// Returns the merged, filtered, reverse-chronological event list for the given patient.
    ///
    /// Always returns HTTP 200 — an empty <see cref="TimelineResponseDto.Events"/> list
    /// is returned when the patient has no matching events (Edge Case 1).
    /// </summary>
    Task<TimelineResponseDto> GetTimelineAsync(
        Guid patientId,
        TimelineQuery query,
        CancellationToken ct = default);
}
