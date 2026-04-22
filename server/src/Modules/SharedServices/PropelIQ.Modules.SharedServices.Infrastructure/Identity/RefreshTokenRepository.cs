using Microsoft.EntityFrameworkCore;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// EF Core-backed refresh token repository.
/// Handles one-time-use rotation and bulk revocation
/// when suspicious token reuse is detected (edge-case AC).
/// </summary>
public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthDbContext _context;

    public RefreshTokenRepository(AuthDbContext context)
        => _context = context;

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task UpdateAsync(RefreshToken token, CancellationToken ct = default)
    {
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        string reason,
        CancellationToken ct = default)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokeReason = reason;
        }

        await _context.SaveChangesAsync(ct);
    }
}
