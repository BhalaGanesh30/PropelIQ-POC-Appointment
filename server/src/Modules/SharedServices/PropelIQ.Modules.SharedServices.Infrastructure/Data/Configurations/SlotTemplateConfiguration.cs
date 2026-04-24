using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class SlotTemplateConfiguration : IEntityTypeConfiguration<SlotTemplate>
{
    public void Configure(EntityTypeBuilder<SlotTemplate> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Location)
            .HasMaxLength(256);

        builder.Property(t => t.DefaultDuration)
            .HasConversion<int>();

        builder.Property(t => t.Type)
            .HasConversion<int>();

        builder.HasIndex(t => new { t.DayOfWeek, t.Type, t.IsActive })
            .HasDatabaseName("ix_slot_templates_dayofweek_type_isactive");
    }
}
