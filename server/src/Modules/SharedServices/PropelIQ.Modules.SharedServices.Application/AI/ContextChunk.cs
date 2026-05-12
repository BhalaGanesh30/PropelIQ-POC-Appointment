namespace PropelIQ.Modules.SharedServices.Application.AI;

/// <summary>
/// A patient-scoped context chunk passed to the AI prompt pipeline.
///
/// Created by the calling service from pgvector retrieval results (e.g., <c>EvidenceChunk</c>)
/// by enriching each chunk with the known <see cref="PatientId"/>. The
/// <see cref="IPatientContextAclFilter"/> validates that every chunk belongs to the
/// expected patient before the chunk content is included in the AI prompt (AC-4, AIR-010).
/// </summary>
/// <param name="FactId">Primary key of the clinical fact (for ACL audit logging).</param>
/// <param name="PatientId">Patient this chunk belongs to — must match the request patient.</param>
/// <param name="FactType">Category: medication | allergy | diagnosis | finding.</param>
/// <param name="Content">Prompt-ready text representation of the fact.</param>
public sealed record ContextChunk(
    Guid FactId,
    Guid PatientId,
    string FactType,
    string Content);
