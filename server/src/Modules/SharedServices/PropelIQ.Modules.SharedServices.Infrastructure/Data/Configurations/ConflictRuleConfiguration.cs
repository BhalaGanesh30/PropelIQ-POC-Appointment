using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="ConflictRule"/>.
/// Maps to the <c>app.conflict_rules</c> table (task_003 migration).
/// </summary>
public sealed class ConflictRuleConfiguration : IEntityTypeConfiguration<ConflictRule>
{
    public void Configure(EntityTypeBuilder<ConflictRule> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.RuleType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.DrugAName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.DrugBName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.Severity)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.Description)
            .IsRequired();

        builder.Property(r => r.Source)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("system");

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.LastUpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("now()");

        // Partial index on active rules for fast type+drug lookup (GetActiveRulesAsync).
        builder.HasIndex(r => new { r.RuleType, r.DrugAName, r.DrugBName })
            .HasDatabaseName("ix_conflict_rules_type_drugs")
            .HasFilter("is_active = true");
    }
}
