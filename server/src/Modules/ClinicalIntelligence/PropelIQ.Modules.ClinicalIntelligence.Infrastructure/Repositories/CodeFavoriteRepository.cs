using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICodeFavoriteRepository"/>.
///
/// Manages <c>app.user_code_favorites</c> rows and joins <c>app.icd_codes</c> /
/// <c>app.cpt_codes</c> for code descriptions.  Code existence is validated before
/// insert to enforce referential integrity at the application layer.
/// </summary>
internal sealed class CodeFavoriteRepository : ICodeFavoriteRepository
{
    private readonly AppDbContext _db;

    public CodeFavoriteRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodeResultDto>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var favorites = await _db.UserCodeFavorites
            .Where(f => f.UserId == userId)
            .ToListAsync(ct);

        if (favorites.Count == 0)
            return [];

        var results = new List<CodeResultDto>(favorites.Count);

        var icdFavorites = favorites
            .Where(f => f.CodeType == "icd10")
            .Select(f => f.Code)
            .ToHashSet();

        var cptFavorites = favorites
            .Where(f => f.CodeType == "cpt")
            .Select(f => f.Code)
            .ToHashSet();

        if (icdFavorites.Count > 0)
        {
            var icdDtos = await _db.IcdCodes
                .Where(c => icdFavorites.Contains(c.Code))
                .Select(c => new CodeResultDto(c.Code, c.Description, "icd10", c.IsDeprecated, true))
                .ToListAsync(ct);

            results.AddRange(icdDtos);
        }

        if (cptFavorites.Count > 0)
        {
            var cptDtos = await _db.CptCodes
                .Where(c => cptFavorites.Contains(c.CptCode))
                .Select(c => new CodeResultDto(c.CptCode, c.Description, "cpt", c.IsDeprecated, true))
                .ToListAsync(ct);

            results.AddRange(cptDtos);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetFavoriteKeysAsync(Guid userId, CancellationToken ct = default)
    {
        // Composite key format: "{codeType}:{code}" — e.g. "icd10:E11.9", "cpt:99213".
        var keys = await _db.UserCodeFavorites
            .Where(f => f.UserId == userId)
            .Select(f => f.CodeType + ":" + f.Code)
            .ToListAsync(ct);

        return [..keys];
    }

    /// <inheritdoc />
    public async Task<bool> AddAsync(Guid userId, string code, string codeType, CancellationToken ct = default)
    {
        // Validate the code exists in the appropriate reference catalog.
        var codeExists = codeType == "icd10"
            ? await _db.IcdCodes.AnyAsync(c => c.Code == code, ct)
            : await _db.CptCodes.AnyAsync(c => c.CptCode == code, ct);

        if (!codeExists)
            return false;

        // Idempotent insert — skip if the favorite already exists.
        var alreadyExists = await _db.UserCodeFavorites
            .AnyAsync(f => f.UserId == userId && f.CodeType == codeType && f.Code == code, ct);

        if (alreadyExists)
            return true;

        _db.UserCodeFavorites.Add(new UserCodeFavorite
        {
            UserId   = userId,
            CodeType = codeType,
            Code     = code,
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(Guid userId, string codeType, string code, CancellationToken ct = default)
    {
        var affected = await _db.UserCodeFavorites
            .Where(f => f.UserId == userId && f.CodeType == codeType && f.Code == code)
            .ExecuteDeleteAsync(ct);

        return affected > 0;
    }
}
