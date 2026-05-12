using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="UserActivityLog"/> (US_061, AC-2, AC-3).
///
/// <para>Table: <c>app.user_activity_logs</c></para>
///
/// Indexes:
/// <list type="bullet">
///   <item>
///     <c>ix_user_activity_logs_user_occurred</c> — composite descending on
///     <c>(user_id, occurred_at)</c> for paginated reverse-chronological history queries (AC-3).
///   </item>
/// </list>
///
/// FK behaviours:
/// <list type="bullet">
///   <item><c>user_id</c> → CASCADE: deleting a user purges their activity log.</item>
///   <item><c>performed_by_user_id</c> → SET NULL: deleting an admin preserves their log entries.</item>
/// </list>
/// </summary>
public sealed class UserActivityLogConfiguration : IEntityTypeConfiguration<UserActivityLog>
{
    public void Configure(EntityTypeBuilder<UserActivityLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.UserId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.EventType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Description)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.OccurredAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(a => a.PerformedByUserId)
            .HasColumnType("uuid");

        // ── FK: user_id → users.id (CASCADE) ─────────────────────────────────
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── FK: performed_by_user_id → users.id (SET NULL) ───────────────────
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.PerformedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Primary query index: per-user reverse chronological (AC-3) ────────
        // Descending on occurred_at to service ORDER BY occurred_at DESC without a sort.
        builder.HasIndex(a => new { a.UserId, a.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_user_activity_logs_user_occurred");
    }
}
