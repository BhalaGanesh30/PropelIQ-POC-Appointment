namespace PropelIQ.Modules.ClinicalIntelligence.Application.Models;

/// <summary>
/// A single clinical entity extracted from a document chunk by the AI pipeline.
/// Passed from the AI layer to the orchestration service before persistence.
/// </summary>
/// <param name="FactType">
/// Category of the entity: <c>medication</c>, <c>allergy</c>, <c>diagnosis</c>, or <c>finding</c>.
/// </param>
/// <param name="Name">Canonical entity name after normalization (e.g. "acetaminophen/tylenol").</param>
/// <param name="Value">Full structured value (e.g. "500mg twice daily" for a medication).</param>
/// <param name="Confidence">Model-reported confidence score in the range 0.0 – 1.0.</param>
/// <param name="SourceText">Verbatim text segment from which the fact was extracted (AIR-004).</param>
public sealed record ExtractedFact(
    string FactType,
    string Name,
    string Value,
    decimal Confidence,
    string SourceText);
