using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="InsuranceProvider"/> reference table
/// (EP-005 US_037 task_003 dependency).
///
/// The full provider list is small and changes rarely; it is cached in Redis
/// under <c>insurance:providers:all</c> for sub-500ms lookups (NFR-002).
/// Seed data: BCBS, Aetna, UHC, Cigna, Humana (EP-005 key deliverables).
/// </summary>
public sealed class InsuranceProviderConfiguration : IEntityTypeConfiguration<InsuranceProvider>
{
    // Stable seed UUIDs — fixed so migrations are idempotent across environments.
    private static readonly Guid BcbsId = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid AetnaId = new("11111111-0000-0000-0000-000000000002");
    private static readonly Guid UhcId = new("11111111-0000-0000-0000-000000000003");
    private static readonly Guid CignaId = new("11111111-0000-0000-0000-000000000004");
    private static readonly Guid HumanaId = new("11111111-0000-0000-0000-000000000005");

    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<InsuranceProvider> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.ProviderCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(p => p.ProviderName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.PolicyNumberPattern)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("now()");

        // Provider codes must be unique for the lookup to work correctly.
        builder.HasIndex(p => p.ProviderCode)
            .IsUnique()
            .HasDatabaseName("ix_insurance_providers_provider_code");

        // Partial index for active providers only (used in validation engine queries).
        builder.HasIndex(p => p.ProviderCode)
            .HasFilter("\"is_active\" = true")
            .HasDatabaseName("ix_insurance_providers_active_code");

        // ── Seed data (EP-005 key deliverables — dummy patterns for dev/test) ─
        // Anonymous objects are used to bypass the `protected set` on BaseEntity.
        builder.HasData(
            new
            {
                Id = BcbsId,
                ProviderCode = "BCBS",
                ProviderName = "Blue Cross Blue Shield",
                PolicyNumberPattern = "^[A-Z]{3}[0-9]{9}$",
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = AetnaId,
                ProviderCode = "AETNA",
                ProviderName = "Aetna",
                PolicyNumberPattern = "^W[0-9]{8,12}$",
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = UhcId,
                ProviderCode = "UHC",
                ProviderName = "UnitedHealthcare",
                PolicyNumberPattern = "^[0-9]{9,11}$",
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = CignaId,
                ProviderCode = "CIGNA",
                ProviderName = "Cigna",
                PolicyNumberPattern = "^U[0-9]{8}$",
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = HumanaId,
                ProviderCode = "HUMANA",
                ProviderName = "Humana",
                PolicyNumberPattern = "^H[A-Z0-9]{6,14}$",
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            }
        );
    }
}
