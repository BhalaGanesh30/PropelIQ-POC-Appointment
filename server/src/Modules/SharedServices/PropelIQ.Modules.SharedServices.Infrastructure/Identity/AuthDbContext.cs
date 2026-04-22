using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// EF Core DbContext dedicated to ASP.NET Core Identity tables.
/// Uses the <c>auth</c> schema to keep authentication data separate from
/// domain data in the <c>app</c> schema managed by <see cref="Data.AppDbContext"/>.
/// </summary>
public sealed class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ActiveSession> ActiveSessions => Set<ActiveSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Isolate all Identity tables under the 'auth' schema.
        builder.HasDefaultSchema("auth");

        builder.Entity<ActiveSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionToken).IsRequired().HasMaxLength(256);
            entity.Property(e => e.TerminationReason).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(512);

            // Composite index for single-session lookup (most common query).
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            // Unique index for token-based lookup (session/extend endpoint).
            entity.HasIndex(e => e.SessionToken).IsUnique();
            // Index supporting the cleanup service cutoff query.
            entity.HasIndex(e => e.LastActivityAt);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Token).IsRequired().HasMaxLength(512);
            entity.Property(t => t.CreatedByIp).HasMaxLength(45);
            entity.Property(t => t.RevokedByIp).HasMaxLength(45);
            entity.Property(t => t.RevokeReason).HasMaxLength(256);
            entity.Property(t => t.ReplacedByToken).HasMaxLength(512);

            entity.HasIndex(t => t.Token).IsUnique();
            entity.HasIndex(t => t.UserId);

            entity.HasOne(t => t.User)
                  .WithMany()
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
