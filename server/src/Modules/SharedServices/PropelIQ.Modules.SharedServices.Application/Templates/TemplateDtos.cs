namespace PropelIQ.Modules.SharedServices.Application.Templates;

// ── List / detail DTOs ──────────────────────────────────────────────────────

/// <summary>Summary row returned in the paginated template list.</summary>
public sealed record TemplateListItemDto(
    Guid     Id,
    string   Name,
    string   Type,
    string   Description,
    int      CurrentVersionNumber,
    DateTimeOffset LastModifiedUtc,
    string   LastModifiedByName);

/// <summary>Full template detail including the current active version.</summary>
public sealed record TemplateDetailDto(
    Guid                Id,
    string              Name,
    string              Type,
    string              Description,
    TemplateVersionDto  CurrentVersion,
    DateTimeOffset      CreatedAt);

/// <summary>Single version snapshot used in detail and history responses.</summary>
public sealed record TemplateVersionDto(
    Guid           Id,
    int            VersionNumber,
    string         Content,
    string?        Subject,
    bool           IsActive,
    DateTimeOffset CreatedAtUtc,
    string         CreatedByName,
    Guid?          RestoredFromVersionId);

// ── Mutation requests ────────────────────────────────────────────────────────

/// <summary>Payload for <c>POST /api/v1/admin/templates/{id}</c> — creates a new immutable version (AC-1).</summary>
public sealed record SaveTemplateRequest(
    string  Content,
    string? Subject);

/// <summary>
/// Payload for <c>POST /api/v1/admin/templates/{id}/preview</c>.
/// Accepts unsaved draft content so the admin can preview before committing (AC-2).
/// </summary>
public sealed record PreviewRequest(
    string  Content,
    string? Subject);

// ── Response DTOs ────────────────────────────────────────────────────────────

/// <summary>
/// Preview rendering result (AC-2).
/// <para>
/// For HTML templates: <see cref="RenderedHtml"/> contains merge-field-substituted markup;
/// <see cref="SmsInfo"/> is null.
/// </para>
/// <para>
/// For SMS templates: <see cref="RenderedHtml"/> contains the plain-text body with
/// substitutions applied; <see cref="SmsInfo"/> carries character-count metadata (edge case 1).
/// </para>
/// </summary>
public sealed record PreviewResponse(
    string   RenderedHtml,
    string?  RenderedSubject,
    SmsInfo? SmsInfo);

/// <summary>
/// SMS character-count metadata returned in preview when <c>Type == "SMS"</c> (edge case 1).
///
/// <para>
/// GSM-7 standard message capacity is 160 characters.  Concatenated (multi-part) SMS uses
/// 153 characters per segment (6-byte UDH overhead per segment).
/// </para>
/// </summary>
public sealed record SmsInfo(
    int  CharacterCount,
    bool IsMultiPart,
    int  EstimatedSegments);

/// <summary>
/// Result of merge-field validation (AC-4, edge cases 1–2).
/// Returned by <c>POST /api/v1/admin/templates/{id}/validate</c>.
/// </summary>
public sealed record TemplateValidationResult(
    bool          IsValid,
    List<string>  InvalidPlaceholders,
    List<string>  OrphanedPlaceholders);

/// <summary>Generic page envelope — consistent with the platform-wide PagedResult pattern.</summary>
public sealed record TemplatePagedResult<T>(
    IReadOnlyList<T> Items,
    int              TotalCount,
    int              Page,
    int              PageSize);
