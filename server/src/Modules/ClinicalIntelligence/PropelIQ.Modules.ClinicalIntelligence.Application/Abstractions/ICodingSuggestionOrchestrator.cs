using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Exceptions;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Orchestrates the full ICD-10 coding suggestion pipeline for a patient (US_049).
///
/// Pipeline order:
///   preflight → ACL retrieval → PII redaction → LLM inference →
///   schema validation → confidence check → citation attachment → persist
/// </summary>
public interface ICodingSuggestionOrchestrator
{
    /// <summary>
    /// Generates up to 3 ICD-10 coding suggestions for the specified patient.
    /// </summary>
    /// <param name="patientId">Patient GUID.</param>
    /// <param name="clinicianId">Authenticated clinician GUID — used as audit actor for PII redaction events (AC-2, US_054).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="CodingSuggestionResponseDto"/> with ranked suggestions.</returns>
    /// <exception cref="InsufficientClinicalDataException">
    /// Thrown when the patient has no extracted clinical facts (Edge Case 2).
    /// </exception>
    Task<CodingSuggestionResponseDto> GenerateSuggestionsAsync(
        Guid patientId,
        Guid clinicianId,
        CancellationToken ct = default);
}
