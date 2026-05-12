namespace PropelIQ.Modules.SharedServices.Application.Templates;

/// <summary>
/// Application service contract for versioned HTML and SMS notification template
/// management (US_062, AC-1–AC-4, edge cases 1–2).
///
/// <para>
/// Every content mutation (save, restore) appends a new immutable <c>TemplateVersion</c>
/// row — existing rows are never updated.  This ensures queued notifications that reference
/// a specific version ID continue rendering the version they were sent with (AC-3).
/// </para>
///
/// Implemented by <c>TemplateManagementService</c> in the Infrastructure layer.
/// </summary>
public interface ITemplateManagementService
{
    /// <summary>
    /// Returns a paginated list of templates, optionally filtered by type ("HTML" or "SMS").
    /// </summary>
    Task<TemplatePagedResult<TemplateListItemDto>> ListAsync(
        string?           typeFilter,
        int               page,
        int               pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Returns full detail for a single template including the current active version.
    /// Throws <see cref="KeyNotFoundException"/> when the template does not exist.
    /// </summary>
    Task<TemplateDetailDto> GetByIdAsync(
        Guid              templateId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full version history for a template in descending version-number order.
    /// </summary>
    Task<List<TemplateVersionDto>> GetVersionsAsync(
        Guid              templateId,
        int               page,
        int               pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Validates content, deactivates the current version, and creates a new active
    /// version with the supplied content (AC-1).
    ///
    /// Throws <see cref="InvalidOperationException"/> with a validation error message
    /// when content contains invalid merge-field placeholders (AC-4).
    /// </summary>
    Task<TemplateVersionDto> SaveAsync(
        Guid                  templateId,
        SaveTemplateRequest   request,
        Guid                  adminId,
        string                adminName,
        CancellationToken     ct = default);

    /// <summary>
    /// Renders the supplied content with sample data substituted for merge-field
    /// tokens (AC-2).  Accepts unsaved draft content — no database write occurs.
    /// </summary>
    Task<PreviewResponse> PreviewAsync(
        Guid              templateId,
        PreviewRequest    request,
        CancellationToken ct = default);

    /// <summary>
    /// Copies the content of an existing version into a new active version (AC-3).
    ///
    /// The restored version has an incremented version number and its
    /// <see cref="Domain.Entities.TemplateVersion.RestoredFromVersionId"/> set to
    /// <paramref name="versionId"/>.  Queued notifications referencing the old version
    /// are unaffected.
    /// </summary>
    Task<TemplateVersionDto> RestoreVersionAsync(
        Guid              templateId,
        Guid              versionId,
        Guid              adminId,
        string            adminName,
        CancellationToken ct = default);

    /// <summary>
    /// Validates the merge-field placeholders in <paramref name="content"/> against
    /// the <c>MergeFieldRegistry</c> (AC-4, edge case 2).
    /// </summary>
    Task<TemplateValidationResult> ValidateAsync(
        string            content,
        CancellationToken ct = default);
}
