using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="EmbeddingSample"/>.
/// Configures the pgvector column type and IVFFlat approximate nearest-neighbour
/// index for the <c>Embedding</c> property (AC-4 validation target).
///
/// IVFFlat is preferred for initial scaffold — HNSW can be substituted in the
/// AI indexing task when production recall/build-time trade-offs are evaluated.
/// </summary>
public sealed class EmbeddingSampleConfiguration : IEntityTypeConfiguration<EmbeddingSample>
{
    public void Configure(EntityTypeBuilder<EmbeddingSample> builder)
    {
        builder.ToTable("embedding_samples", "app");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.Property(e => e.ContentRef)
            .IsRequired()
            .HasMaxLength(512);

        // vector(1536): 1536-dimension float32 vector column for OpenAI-compatible embeddings.
        // Matches AIR-004 retrieval workload embedding dimensions.
        builder.Property(e => e.Embedding)
            .HasColumnType("vector(1536)");

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()");

        // IVFFlat index with cosine distance operator for semantic similarity search.
        // Lists=100 is a sensible starting value; reconfigure when dataset > 1M rows.
        builder.HasIndex(e => e.Embedding)
            .HasMethod("ivfflat")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("lists", 100);
    }
}
