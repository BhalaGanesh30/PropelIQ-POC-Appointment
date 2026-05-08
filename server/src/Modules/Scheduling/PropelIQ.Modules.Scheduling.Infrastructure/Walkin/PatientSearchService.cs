using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Walkin.Dto;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Walkin;

/// <summary>
/// Implements <see cref="IPatientSearchService"/> for walk-in disambiguation
/// (EP-004 US_033 Edge Case 1).
///
/// Searches on first + last name (concatenated) and preferred phone using
/// PostgreSQL's case-insensitive <c>ILIKE</c> via <c>EF.Functions.ILike</c>.
/// Results limited to top 10 ordered by last name, then first name.
/// </summary>
public sealed class PatientSearchService : IPatientSearchService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PatientSearchService> _logger;

    public PatientSearchService(AppDbContext db, ILogger<PatientSearchService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientSearchResultDto>> SearchAsync(
        string query,
        CancellationToken ct = default)
    {
        // Normalise the search term — the minimum 2-char guard is enforced at the
        // controller boundary so the service operates without duplicating that check.
        var term = $"%{query.Trim()}%";

        _logger.LogDebug("Patient search: query='{Query}'", query);

        // Single pass: ILike on full name and phone.
        // ContactPreferences is an EF Core owned entity stored as a JSON column
        // ("contact_preferences") — EF Core 8 supports owned-entity property
        // access in LINQ queries against Npgsql JSON columns.
        var results = await _db.Patients
            .AsNoTracking()
            .Where(p =>
                EF.Functions.ILike(p.FirstName + " " + p.LastName, term)
                || EF.Functions.ILike(p.LastName, term)
                || (p.ContactPreferences.PreferredPhone != null
                    && EF.Functions.ILike(p.ContactPreferences.PreferredPhone, term)))
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Take(10)
            .Select(p => new PatientSearchResultDto
            {
                PatientId   = p.Id,
                FirstName   = p.FirstName,
                LastName    = p.LastName,
                DateOfBirth = p.DateOfBirth,
                Phone       = p.ContactPreferences.PreferredPhone,
            })
            .ToListAsync(ct);

        return results;
    }
}
