using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="InsuranceValidationResult"/> audit table
/// (EP-005 US_037 task_003 dependency).
///
/// Every call to <c>InsuranceValidationService.ValidateAsync</c> writes one row here.
/// Staff review the table to action <c>ValidationFailed</c> records (AC-4).
/// The background retry service polls for <c>ValidationPending</c> rows where
/// <c>retry_count &lt; 3</c> (Edge Case 1).
/// </summary>
public sealed class InsuranceValidationResultConfiguration
    : IEntityTypeConfiguration<InsuranceValidationResult>
{
    public void Configure(EntityTypeBuilder<InsuranceValidationResult> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.PatientId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.PolicyNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(r => r.ProviderCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.Tier)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(r => r.WarningsJson)
            .HasColumnType("text");

        builder.Property(r => r.RetryCount)
            .HasDefaultValue(0);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(r => r.UpdatedAt)
            .HasDefaultValueSql("now()");

        // Index supporting the retry-service query: status = 'ValidationPending' AND retry_count < 3.
        builder.HasIndex(r => new { r.Status, r.RetryCount })
            .HasDatabaseName("ix_insurance_validation_results_status_retry");

        // Index for staff review queue: filter/sort by patient.
        builder.HasIndex(r => r.PatientId)
            .HasDatabaseName("ix_insurance_validation_results_patient_id");
    }
}
