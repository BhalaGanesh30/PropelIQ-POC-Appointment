using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// ACL-filtered evidence retrieval service for the coding suggestion pipeline (AIR-010).
///
/// Embeds a query text via <c>text-embedding-3-small</c> and performs an HNSW
/// cosine-distance search against <c>clinical_facts.embedding</c> scoped strictly
/// to the given patient — no cross-patient context leakage (AIR-010).
/// </summary>
public interface IEvidenceRetrievalService
{
    /// <summary>
    /// Returns the top-<paramref name="topK"/> evidence chunks for the patient ranked
    /// by cosine similarity to the embedded <paramref name="queryText"/>.
    ///
    /// Only facts belonging to <paramref name="patientId"/> are considered (ACL filter).
    /// Returns an empty list when no embeddings are available.
    /// </summary>
    Task<IReadOnlyList<EvidenceChunk>> RetrieveAsync(
        Guid patientId,
        string queryText,
        int topK = 10,
        CancellationToken ct = default);
}
