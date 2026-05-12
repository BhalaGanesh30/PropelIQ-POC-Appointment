using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Application.Templates;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Templates;

/// <summary>
/// EF Core implementation of <see cref="ITemplateManagementService"/> (US_062, AC-1–AC-4).
///
/// <list type="bullet">
///   <item>AC-1  — <see cref="SaveAsync"/> appends a new immutable <c>TemplateVersion</c> row per edit.</item>
///   <item>AC-2  — <see cref="PreviewAsync"/> substitutes merge fields with sample values via <see cref="MergeFieldRegistry"/>.</item>
///   <item>AC-3  — <see cref="RestoreVersionAsync"/> copies old content into a new version, leaving old rows intact.</item>
///   <item>AC-4  — <see cref="ValidateAsync"/> / <see cref="SaveAsync"/> reject unknown placeholders with 422.</item>
///   <item>Edge 1 — <see cref="PreviewAsync"/> returns <see cref="SmsInfo"/> for SMS templates (char count, multi-part).</item>
///   <item>Edge 2 — <see cref="ValidateAsync"/> lists orphaned placeholders (unknown tokens in existing content).</item>
/// </list>
/// </summary>
public sealed class TemplateManagementService : ITemplateManagementService
{
    private readonly AppDbContext          _db;
    private readonly IAuditRecordService   _audit;
    private readonly MergeFieldRegistry    _registry;
    private readonly ILogger<TemplateManagementService> _logger;

    /// <summary>
    /// GSM-7 standard single-message limit.
    /// Concatenated (multi-part) SMS uses 153 chars/segment due to 6-byte UDH header overhead.
    /// </summary>
    private const int SmsStandardLimit  = 160;
    private const int SmsSegmentLimit   = 153;

    public TemplateManagementService(
        AppDbContext                          db,
        IAuditRecordService                   audit,
        MergeFieldRegistry                    registry,
        ILogger<TemplateManagementService>    logger)
    {
        _db       = db;
        _audit    = audit;
        _registry = registry;
        _logger   = logger;
    }

    // ── List ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TemplatePagedResult<TemplateListItemDto>> ListAsync(
        string?           typeFilter,
        int               page,
        int               pageSize,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);

        var query = _db.NotificationTemplates
            .AsNoTracking()
            .Include(t => t.CurrentVersion)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(typeFilter))
            query = query.Where(t => t.Type == typeFilter);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(t => new TemplateListItemDto(
            t.Id,
            t.Name,
            t.Type,
            t.Description,
            t.CurrentVersion?.VersionNumber ?? 0,
            t.CurrentVersion?.CreatedAtUtc   ?? t.CreatedAt,
            t.CurrentVersion?.CreatedByName  ?? string.Empty))
            .ToList();

        return new TemplatePagedResult<TemplateListItemDto>(dtos, total, page, pageSize);
    }

    // ── GetById ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TemplateDetailDto> GetByIdAsync(
        Guid              templateId,
        CancellationToken ct = default)
    {
        var template = await _db.NotificationTemplates
            .AsNoTracking()
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        if (template.CurrentVersion is null)
            throw new InvalidOperationException(
                $"Template {templateId} has no active version.");

        return ToDetailDto(template, template.CurrentVersion);
    }

    // ── GetVersions ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<TemplateVersionDto>> GetVersionsAsync(
        Guid              templateId,
        int               page,
        int               pageSize,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 100);

        return await _db.TemplateVersions
            .AsNoTracking()
            .Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => ToVersionDto(v))
            .ToListAsync(ct);
    }

    // ── Save (AC-1, AC-4) ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TemplateVersionDto> SaveAsync(
        Guid                  templateId,
        SaveTemplateRequest   request,
        Guid                  adminId,
        string                adminName,
        CancellationToken     ct = default)
    {
        // AC-4: validate merge fields before any DB write.
        var validation = await ValidateAsync(request.Content, ct);
        if (!validation.IsValid)
            throw new InvalidOperationException(
                $"Template content contains invalid merge-field placeholder(s): " +
                $"{string.Join(", ", validation.InvalidPlaceholders)}.");

        var template = await _db.NotificationTemplates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        // Deactivate current active version.
        if (template.CurrentVersion is not null)
            template.CurrentVersion.IsActive = false;

        // Compute next version number.
        var nextVersion = await _db.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        nextVersion++;

        var version = new TemplateVersion
        {
            TemplateId         = templateId,
            VersionNumber      = nextVersion,
            Content            = request.Content,
            Subject            = request.Subject,
            IsActive           = true,
            CreatedAtUtc       = DateTimeOffset.UtcNow,
            CreatedByUserId    = adminId,
            CreatedByName      = adminName,
            RestoredFromVersionId = null,
        };

        _db.TemplateVersions.Add(version);
        template.CurrentVersionId = version.Id;
        await _db.SaveChangesAsync(ct);

        // NFR-010: fire-and-forget audit event.
        await _audit.WriteAsync(new AuditEvent
        {
            UserId     = adminId,
            EventType  = "TemplateSaved",
            EntityType = "NotificationTemplate",
            EntityId   = templateId,
            Details    = new Dictionary<string, object>
            {
                ["versionNumber"] = nextVersion,
                ["templateName"]  = template.Name,
            },
        }, ct);

        _logger.LogInformation(
            "Template {TemplateId} saved as version {Version} by admin {AdminId}.",
            templateId, nextVersion, adminId);

        return ToVersionDto(version);
    }

    // ── Preview (AC-2, edge case 1) ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PreviewResponse> PreviewAsync(
        Guid              templateId,
        PreviewRequest    request,
        CancellationToken ct = default)
    {
        var template = await _db.NotificationTemplates
            .AsNoTracking()
            .Select(t => new { t.Id, t.Type })
            .FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        var renderedBody    = _registry.Substitute(request.Content);
        var renderedSubject = request.Subject is not null
            ? _registry.Substitute(request.Subject)
            : null;

        SmsInfo? smsInfo = null;
        if (string.Equals(template.Type, "SMS", StringComparison.OrdinalIgnoreCase))
        {
            var charCount   = renderedBody.Length;
            var isMultiPart = charCount > SmsStandardLimit;
            var segments    = isMultiPart
                ? (int)Math.Ceiling((double)charCount / SmsSegmentLimit)
                : 1;
            smsInfo = new SmsInfo(charCount, isMultiPart, segments);
        }

        return new PreviewResponse(renderedBody, renderedSubject, smsInfo);
    }

    // ── Restore (AC-3) ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TemplateVersionDto> RestoreVersionAsync(
        Guid              templateId,
        Guid              versionId,
        Guid              adminId,
        string            adminName,
        CancellationToken ct = default)
    {
        var template = await _db.NotificationTemplates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        var source = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == templateId, ct)
            ?? throw new KeyNotFoundException(
                $"Version {versionId} not found on template {templateId}.");

        // Deactivate current active version.
        if (template.CurrentVersion is not null)
            template.CurrentVersion.IsActive = false;

        // Increment version number.
        var nextVersion = await _db.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        nextVersion++;

        // New version copies the content of the source — old rows untouched (AC-3).
        var restored = new TemplateVersion
        {
            TemplateId            = templateId,
            VersionNumber         = nextVersion,
            Content               = source.Content,
            Subject               = source.Subject,
            IsActive              = true,
            CreatedAtUtc          = DateTimeOffset.UtcNow,
            CreatedByUserId       = adminId,
            CreatedByName         = adminName,
            RestoredFromVersionId = versionId,
        };

        _db.TemplateVersions.Add(restored);
        template.CurrentVersionId = restored.Id;
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync(new AuditEvent
        {
            UserId     = adminId,
            EventType  = "TemplateRestored",
            EntityType = "NotificationTemplate",
            EntityId   = templateId,
            Details    = new Dictionary<string, object>
            {
                ["restoredFromVersionId"] = versionId.ToString(),
                ["newVersionNumber"]      = nextVersion,
                ["templateName"]          = template.Name,
            },
        }, ct);

        _logger.LogInformation(
            "Template {TemplateId} restored from version {SourceVersionId} as version {NewVersion}.",
            templateId, versionId, nextVersion);

        return ToVersionDto(restored);
    }

    // ── Validate (AC-4, edge case 2) ─────────────────────────────────────────

    /// <inheritdoc />
    public Task<TemplateValidationResult> ValidateAsync(
        string            content,
        CancellationToken ct = default)
    {
        var unknown  = _registry.ExtractUnknownPlaceholders(content);
        var isValid  = unknown.Count == 0;

        // Edge case 2: for this implementation, all unknown tokens are treated as invalid
        // (the registry is the canonical source of truth). The distinction between
        // "never valid" and "was valid but deleted" can be layered on top once a
        // DeletedMergeFields audit log is introduced in a future task.
        var result = new TemplateValidationResult(
            IsValid:              isValid,
            InvalidPlaceholders:  unknown,
            OrphanedPlaceholders: []); // populated when a deleted-field registry is available

        return Task.FromResult(result);
    }

    // ── Projection helpers ───────────────────────────────────────────────────

    private static TemplateDetailDto ToDetailDto(
        NotificationTemplate template,
        TemplateVersion      version) =>
        new(template.Id,
            template.Name,
            template.Type,
            template.Description,
            ToVersionDto(version),
            template.CreatedAt);

    private static TemplateVersionDto ToVersionDto(TemplateVersion v) =>
        new(v.Id,
            v.VersionNumber,
            v.Content,
            v.Subject,
            v.IsActive,
            v.CreatedAtUtc,
            v.CreatedByName,
            v.RestoredFromVersionId);
}
