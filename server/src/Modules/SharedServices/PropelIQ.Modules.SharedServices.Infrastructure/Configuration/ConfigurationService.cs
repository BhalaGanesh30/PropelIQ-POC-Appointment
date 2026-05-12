using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Application.Configuration;
using PropelIQ.Modules.SharedServices.Application.Configuration.Validators;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using System.Text.Json;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Configuration;

/// <summary>
/// Implements <see cref="IConfigurationService"/> — versioned, append-only configuration
/// persistence with optimistic concurrency control (OCC), JSONB diff tracking, and audit logging
/// (US_059, AC-1–AC-4, edge cases 1–2).
///
/// <para>
/// Every <see cref="UpdateAsync"/> call inserts a new <c>configuration_versions</c> row;
/// existing rows are never modified. The <see cref="ConfigurationCacheService"/> singleton
/// is updated on every successful write so consumers read from cache at zero latency (edge case 2).
/// </para>
/// </summary>
public sealed class ConfigurationService : IConfigurationService
{
    private readonly AppDbContext _db;
    private readonly IAuditRecordService _audit;
    private readonly ConfigurationCacheService _cache;
    private readonly ILogger<ConfigurationService> _logger;

    /// <summary>
    /// Stateless, category-keyed validators. Instantiated once per process; no DI injection
    /// required since these validators hold no mutable state.
    /// </summary>
    private static readonly IReadOnlyDictionary<ConfigurationCategory, IValidator<Dictionary<string, object>>> Validators =
        new Dictionary<ConfigurationCategory, IValidator<Dictionary<string, object>>>
        {
            [ConfigurationCategory.SessionPolicy]         = new SessionPolicyValidator(),
            [ConfigurationCategory.SlotTemplates]         = new SlotTemplateValidator(),
            [ConfigurationCategory.ReminderRules]         = new ReminderRuleValidator(),
            [ConfigurationCategory.CommunicationTemplates] = new CommunicationTemplateValidator()
        };

    public ConfigurationService(
        AppDbContext db,
        IAuditRecordService audit,
        ConfigurationCacheService cache,
        ILogger<ConfigurationService> logger)
    {
        _db     = db;
        _audit  = audit;
        _cache  = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ConfigurationSnapshot> GetCurrentAsync(
        ConfigurationCategory category,
        CancellationToken ct = default)
    {
        if (_cache.TryGet(category, out var cached) && cached is not null)
            return cached;

        var entity = await _db.ConfigurationVersions
            .Where(v => v.Category == category.ToString())
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return BuildDefaultSnapshot(category);

        var snapshot = ToSnapshot(entity, category);
        _cache.UpdateCache(category, snapshot);
        return snapshot;
    }

    /// <inheritdoc />
    public async Task<ConfigurationUpdateResult> UpdateAsync(
        ConfigurationCategory category,
        ConfigurationUpdateRequest request,
        CancellationToken ct = default)
    {
        // ── Load current version (null = no prior state for this category) ────
        var current = await _db.ConfigurationVersions
            .Where(v => v.Category == category.ToString())
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        var currentVersion = current?.VersionNumber ?? 0;

        // ── Optimistic concurrency check (edge case 1) ────────────────────────
        if (currentVersion != request.ExpectedVersion)
        {
            _logger.LogInformation(
                "Configuration OCC conflict for {Category}: expected v{Expected}, actual v{Actual}",
                category, request.ExpectedVersion, currentVersion);

            return new ConfigurationUpdateResult
            {
                Success          = false,
                ConflictDetected = true,
                CurrentValue     = current is null
                    ? BuildDefaultSnapshot(category)
                    : ToSnapshot(current, category)
            };
        }

        // ── Validate submitted values (AC-2) ─────────────────────────────────
        var errors = await ValidateValuesAsync(category, request.Values, ct);
        if (errors.Count > 0)
            return new ConfigurationUpdateResult { Success = false, ValidationErrors = errors };

        // ── Persist new append-only version row (AC-1) ────────────────────────
        var newVersion = new ConfigurationVersion
        {
            Category            = category.ToString(),
            VersionNumber       = currentVersion + 1,
            ValuesJson          = JsonSerializer.Serialize(request.Values),
            PreviousValuesJson  = current?.ValuesJson,
            ChangedByAdminId    = request.AdminId,
            ChangedByName       = request.AdminName,
            ChangedAtUtc        = DateTime.UtcNow
        };

        _db.ConfigurationVersions.Add(newVersion);
        await _db.SaveChangesAsync(ct);

        // ── Write-through cache update ────────────────────────────────────────
        var snapshot = ToSnapshot(newVersion, category);
        _cache.UpdateCache(category, snapshot);

        // ── Audit log (US_056 IAuditRecordService) ────────────────────────────
        await _audit.WriteAsync(new AuditEvent
        {
            UserId     = request.AdminId,
            EventType  = "ConfigChanged",
            EntityType = "Configuration",
            EntityId   = newVersion.Id,
            Details    = new Dictionary<string, object>
            {
                ["category"]      = category.ToString(),
                ["versionNumber"] = newVersion.VersionNumber,
                ["changedBy"]     = request.AdminName
            }
        }, ct);

        _logger.LogInformation(
            "Configuration {Category} updated to v{Version} by {Admin}",
            category, newVersion.VersionNumber, request.AdminName);

        return new ConfigurationUpdateResult
        {
            Success       = true,
            VersionId     = newVersion.Id,
            VersionNumber = newVersion.VersionNumber
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationVersionDto>> GetHistoryAsync(
        ConfigurationCategory category,
        CancellationToken ct = default)
    {
        var versions = await _db.ConfigurationVersions
            .Where(v => v.Category == category.ToString())
            .OrderByDescending(v => v.VersionNumber)
            .AsNoTracking()
            .ToListAsync(ct);

        return versions
            .Select(v => new ConfigurationVersionDto
            {
                VersionId             = v.Id,
                VersionNumber         = v.VersionNumber,
                Category              = category,
                ChangedAtUtc          = v.ChangedAtUtc,
                ChangedByAdminId      = v.ChangedByAdminId,
                ChangedByName         = v.ChangedByName,
                Values                = DeserializeValues(v.ValuesJson),
                PreviousValues        = v.PreviousValuesJson is null
                    ? null
                    : DeserializeValues(v.PreviousValuesJson),
                RestoredFromVersionId = v.RestoredFromVersionId
            })
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<ConfigurationUpdateResult> RestoreVersionAsync(
        ConfigurationCategory category,
        Guid versionId,
        Guid adminId,
        CancellationToken ct = default)
    {
        // ── Load target historical version (AC-4) ─────────────────────────────
        var target = await _db.ConfigurationVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.Id == versionId && v.Category == category.ToString(),
                ct);

        if (target is null)
            return new ConfigurationUpdateResult
            {
                Success          = false,
                ValidationErrors = [$"Version {versionId} not found for category {category}."]
            };

        var targetValues = DeserializeValues(target.ValuesJson);

        // ── Validate restored snapshot against current rules (AC-4) ──────────
        var errors = await ValidateValuesAsync(category, targetValues, ct);
        if (errors.Count > 0)
            return new ConfigurationUpdateResult { Success = false, ValidationErrors = errors };

        // ── Load current to determine next version number ─────────────────────
        var current = await _db.ConfigurationVersions
            .Where(v => v.Category == category.ToString())
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        var newVersion = new ConfigurationVersion
        {
            Category              = category.ToString(),
            VersionNumber         = (current?.VersionNumber ?? 0) + 1,
            ValuesJson            = target.ValuesJson,
            PreviousValuesJson    = current?.ValuesJson,
            ChangedByAdminId      = adminId,
            ChangedByName         = "Rollback",
            ChangedAtUtc          = DateTime.UtcNow,
            RestoredFromVersionId = versionId
        };

        _db.ConfigurationVersions.Add(newVersion);
        await _db.SaveChangesAsync(ct);

        var snapshot = ToSnapshot(newVersion, category);
        _cache.UpdateCache(category, snapshot);

        await _audit.WriteAsync(new AuditEvent
        {
            UserId     = adminId,
            EventType  = "ConfigChanged",
            EntityType = "Configuration",
            EntityId   = newVersion.Id,
            Details    = new Dictionary<string, object>
            {
                ["category"]              = category.ToString(),
                ["versionNumber"]         = newVersion.VersionNumber,
                ["restoredFromVersionId"] = versionId.ToString()
            }
        }, ct);

        _logger.LogInformation(
            "Configuration {Category} rolled back to v{RestoredVersion} (new v{NewVersion})",
            category, target.VersionNumber, newVersion.VersionNumber);

        return new ConfigurationUpdateResult
        {
            Success       = true,
            VersionId     = newVersion.Id,
            VersionNumber = newVersion.VersionNumber
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<string>> ValidateValuesAsync(
        ConfigurationCategory category,
        Dictionary<string, object> values,
        CancellationToken ct)
    {
        if (!Validators.TryGetValue(category, out var validator))
            return [];

        var result = await validator.ValidateAsync(values, ct);
        return result.IsValid
            ? []
            : result.Errors.Select(e => e.ErrorMessage).ToArray();
    }

    private static ConfigurationSnapshot BuildDefaultSnapshot(ConfigurationCategory category) =>
        new()
        {
            VersionId     = Guid.Empty,
            VersionNumber = 0,
            Category      = category,
            Values        = [],
            UpdatedAtUtc  = DateTime.MinValue,
            UpdatedByName = "System"
        };

    private static ConfigurationSnapshot ToSnapshot(
        ConfigurationVersion entity,
        ConfigurationCategory category) =>
        new()
        {
            VersionId     = entity.Id,
            VersionNumber = entity.VersionNumber,
            Category      = category,
            Values        = DeserializeValues(entity.ValuesJson),
            UpdatedAtUtc  = entity.ChangedAtUtc,
            UpdatedByName = entity.ChangedByName
        };

    private static Dictionary<string, object> DeserializeValues(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
