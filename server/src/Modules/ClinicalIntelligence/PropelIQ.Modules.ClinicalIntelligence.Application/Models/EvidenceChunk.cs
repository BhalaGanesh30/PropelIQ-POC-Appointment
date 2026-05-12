namespace PropelIQ.Modules.ClinicalIntelligence.Application.Models;

/// <summary>
/// Internal evidence chunk retrieved from the pgvector HNSW index for a patient
/// (AIR-010 ACL-filtered retrieval). Carries the full fact data needed to build
/// both the LLM prompt context and the <c>ClinicalFactCitationDto</c> response.
/// </summary>
/// <param name="FactId">Primary key of the clinical fact.</param>
/// <param name="DocumentId">Document FK — used when inserting coding decision rows.</param>
/// <param name="FactType">Category: medication | allergy | diagnosis | finding.</param>
/// <param name="Name">Canonical entity name.</param>
/// <param name="Value">Full structured value.</param>
/// <param name="FactDate">Optional clinical date associated with the fact.</param>
/// <param name="Distance">Cosine distance from query vector (lower = more similar).</param>
public sealed record EvidenceChunk(
    Guid FactId,
    Guid DocumentId,
    string FactType,
    string Name,
    string Value,
    DateTimeOffset? FactDate,
    double Distance);
