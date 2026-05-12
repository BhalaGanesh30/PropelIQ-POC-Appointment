using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="UserCodeFavorite"/> → <c>app.user_code_favorites</c> (US_052, task_003).
///
/// Composite PK: (user_id, code_type, code).
/// </summary>
public sealed class UserCodeFavoriteConfiguration : IEntityTypeConfiguration<UserCodeFavorite>
{
    public void Configure(EntityTypeBuilder<UserCodeFavorite> builder)
    {
        builder.ToTable("user_code_favorites", "app");

        // Composite PK: one user cannot favorite the same code+type twice.
        builder.HasKey(f => new { f.UserId, f.CodeType, f.Code });

        builder.Property(f => f.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(f => f.CodeType)
            .HasColumnName("code_type")
            .HasColumnType("character varying(10)")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(f => f.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        // FK to the authenticated user (clinician).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on user_id — accelerates GetFavoriteKeysAsync which filters by user.
        builder.HasIndex(f => f.UserId)
            .HasDatabaseName("ix_user_code_favorites_user_id");
    }
}
