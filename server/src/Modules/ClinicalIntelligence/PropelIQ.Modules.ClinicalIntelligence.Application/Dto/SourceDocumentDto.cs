namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Source document traceability information attached to each clinical fact (AC-2, AC-3).
/// </summary>
public sealed record SourceDocumentDto
{
    /// <summary>GUID of the source clinical document.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>User-facing display name of the document (falls back to filename when null).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the document was uploaded (AC-3).</summary>
    public DateTimeOffset UploadedAt { get; init; }
}
