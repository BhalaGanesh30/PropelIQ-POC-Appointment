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
        });

        builder.HasIndex(a => a.ActorUserId);
        builder.HasIndex(a => a.OccurredAt);
    }
}
