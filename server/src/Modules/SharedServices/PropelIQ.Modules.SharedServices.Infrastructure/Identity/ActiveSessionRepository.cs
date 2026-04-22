using Microsoft.EntityFrameworkCore;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// EF Core-backed active-session repository.
/// Uses <see cref="AuthDbContext"/> so session rows live in the 'auth' schema
/// alongside Identity and RefreshToken tables.
/// </summary>
public sealed class ActiveSessionRepository : IActiveSessionRepository
{
    private readonly AuthDbContext _context;

    public ActiveSessionRepository(AuthDbContext context)
        => _context = context;

    public async Task<ActiveSession?> GetActiveByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await _context.ActiveSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, ct);

    public async Task<ActiveSession?> GetBySessionTokenAsync(
        string sessionToken, CancellationToken ct = default)
        => await _context.ActiveSessions
            .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.IsActive, ct);

    public async Task AddAsync(ActiveSession session, CancellationToken ct = default)
    {
        _context.ActiveSessions.Add(session);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ActiveSession session, CancellationToken ct = default)
    {
        _context.ActiveSessions.Update(session);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ActiveSession>> GetExpiredSessionsAsync(
        DateTime cutoff, CancellationToken ct = default)
        => await _context.ActiveSessions
            .Where(s => s.IsActive && s.LastActivityAt <= cutoff)
            .ToListAsync(ct);
}
