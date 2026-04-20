using Pgvector;

namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Sample entity with a pgvector column used to validate AC-4:
/// vector column creation and the pgvector <-> distance operator query.
///
/// This entity will be superseded by domain-specific embedding entities in
/// EP-AI (ClinicalIntelligence module). It serves as the integration smoke-test
/// for the pgvector + EF Core stack during project foundation setup.
///
/// Embedding dimensions: 1536 matches OpenAI text-embedding-3-small output
/// and is aligned with AIR-004 retrieval workload requirements.
/// </summary>
public class EmbeddingSample
{
    public Guid Id { get; set; }

    /// <summary>Source content that was embedded (truncated reference, not the full text).</summary>
    public string ContentRef { get; set; } = string.Empty;

    /// <summary>
    /// 1536-dimension embedding vector.
    /// Nullable: embedding may not yet be computed for newly ingested content.
    /// </summary>
    public Vector? Embedding { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
