using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.EventType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.ActorUserId)
            .HasColumnType("uuid");

        builder.Property(a => a.TargetEntityId)
            .HasColumnType("uuid");

        builder.Property(a => a.TargetEntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.OwnsOne(a => a.Details, d =>
        {
            d.ToJson();
            d.Property(x => x.IpAddress).HasMaxLength(45);
            d.Property(x => x.UserAgent).HasMaxLength(512);
            d.Property(x => x.ChangeDescription).HasMaxLength(2000);
            // Dictionary<string,string> is not supported by Npgsql EF Core inside
            // ToJson() owned entities. Ignore it; callers never set Metadata in
            // the current audit writes so no data is lost.
            d.Ignore(x => x.Metadata);
        });

        builder.HasIndex(a => a.ActorUserId);
        builder.HasIndex(a => a.OccurredAt);

        // ── Override-specific columns (EP-004 US_034 task_003, DR-007 additive) ──────

        builder.Property(a => a.OverrideConstraintType)
            .IsRequired(false)
            .HasMaxLength(50);

        builder.Property(a => a.OverrideReason)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(a => a.OverrideAction)
            .IsRequired(false)
            .HasMaxLength(20);

        // B-tree index on event_type for AC-4 filtered override queries.
        builder.HasIndex(a => a.EventType)
            .HasDatabaseName("ix_audit_records_event_type");

        // Composite index (event_type, occurred_at DESC) for time-range
        // filtered admin audit log queries (AC-4 + NFR-010 query performance).
        builder.HasIndex(a => new { a.EventType, a.OccurredAt })
            .HasDatabaseName("ix_audit_records_event_type_occurred_at")
            .IsDescending(false, true);
    }
}
