using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="DisclosureRequest"/> (US_057, AC-2, AC-3).
///
/// Table: <c>app.disclosure_requests</c>
/// Indexes:
/// - (patient_id, created_at DESC) — patient list queries.
/// - (status) partial WHERE status NOT IN ('Delivered','Rejected') — staff pending-review queue.
/// </summary>
public sealed class DisclosureRequestConfiguration : IEntityTypeConfiguration<DisclosureRequest>
{
    public void Configure(EntityTypeBuilder<DisclosureRequest> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.PatientId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(d => d.FromDateUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(d => d.ToDateUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.CompiledAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.ReviewedBy)
            .HasColumnType("uuid");

        builder.Property(d => d.ReviewedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.ReviewNotes)
            .HasMaxLength(1000);

        builder.Property(d => d.DeliveredAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.DeliveryMethod)
            .HasMaxLength(20);

        builder.Property(d => d.ReportId)
            .HasColumnType("uuid");

        // One-to-one: DisclosureRequest → DisclosureReport (nullable until compiled).
        builder.HasOne(d => d.Report)
            .WithOne(r => r.Request)
            .HasForeignKey<DisclosureRequest>(d => d.ReportId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(d => new { d.PatientId, d.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_disclosure_requests_patient_created");

        builder.HasIndex(d => d.Status)
            .HasFilter("status NOT IN ('Delivered','Rejected')")
            .HasDatabaseName("ix_disclosure_requests_active_status");
    }
}
