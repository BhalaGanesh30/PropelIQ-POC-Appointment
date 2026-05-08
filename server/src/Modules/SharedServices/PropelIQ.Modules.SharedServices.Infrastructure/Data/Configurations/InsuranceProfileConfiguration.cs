using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class InsuranceProfileConfiguration : IEntityTypeConfiguration<InsuranceProfile>
{
    public void Configure(EntityTypeBuilder<InsuranceProfile> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.PayerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.MemberId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.VerificationStatus)
            .IsRequired()
            .HasMaxLength(50);

        // EP-005 US_037 extended fields (task_002).
        builder.Property(i => i.ProviderCode)
            .HasMaxLength(20);

        builder.Property(i => i.GroupNumber)
            .HasMaxLength(30);

        builder.Property(i => i.CardImageFrontPath)
            .HasMaxLength(500);

        builder.Property(i => i.CardImageBackPath)
            .HasMaxLength(500);

        // EP-005 US_038: Cloudflare R2 object key columns (task_003).
        // Separate from the legacy path columns; stores R2 object keys (max 255 chars).
        builder.Property(i => i.CardImageFrontKey)
            .HasColumnName("card_image_front_key")
            .HasMaxLength(255);

        builder.Property(i => i.CardImageBackKey)
            .HasColumnName("card_image_back_key")
            .HasMaxLength(255);

        // EP-005 US_038: AES-256 field-level encryption columns (task_001).
        // Max length: Base64(16-byte IV + up to ~2048 byte ciphertext) ~ 2756 chars.
        builder.Property(i => i.EncryptedPolicyNumber).HasMaxLength(512);
        builder.Property(i => i.PolicyNumberHmac).HasMaxLength(128);
        builder.Property(i => i.EncryptedProviderName).HasMaxLength(512);
        builder.Property(i => i.ProviderNameHmac).HasMaxLength(128);
        builder.Property(i => i.EncryptedGroupNumber).HasMaxLength(512);
        builder.Property(i => i.GroupNumberHmac).HasMaxLength(128);

        builder.Property(i => i.KeyVersion)
            .HasDefaultValue(0);

        // Index supporting the key-rotation query: WHERE key_version < @current.
        builder.HasIndex(i => i.KeyVersion)
            .HasDatabaseName("ix_insurance_profiles_key_version");

        builder.HasOne(i => i.Patient)
            .WithMany(p => p.InsuranceProfiles)
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(i => i.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(i => i.UpdatedAt)
            .HasDefaultValueSql("now()");
    }
}
