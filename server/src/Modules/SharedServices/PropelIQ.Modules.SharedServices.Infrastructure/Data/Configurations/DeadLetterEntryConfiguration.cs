using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class DeadLetterEntryConfiguration : IEntityTypeConfiguration<DeadLetterEntry>
{
    public void Configure(EntityTypeBuilder<DeadLetterEntry> builder)
    {
        builder.ToTable("ocr_dead_letter_queue");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.DocumentId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(e => e.ErrorMessage)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.StackTrace)
            .HasColumnType("text");

        builder.Property(e => e.RetryCount)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()");

        // Index to look up dead-letter entries by document
        builder.HasIndex(e => e.DocumentId)
            .HasDatabaseName("ix_ocr_dead_letter_queue_document_id");
    }
}
