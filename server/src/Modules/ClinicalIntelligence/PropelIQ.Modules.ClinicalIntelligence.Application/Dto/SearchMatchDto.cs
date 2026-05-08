namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// A single text match found during full-text search over OCR-extracted content.
/// Returned as part of <see cref="DocumentSearchResponse"/>.
/// </summary>
public sealed class SearchMatchDto
{
    /// <summary>The matched text snippet (may contain surrounding context).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Page number within the document, if determinable. Null for plain-text OCR.</summary>
    public int? PageNumber { get; set; }

    /// <summary>Zero-based character offset of the match within <c>extracted_text</c>.</summary>
    public int Position { get; set; }

    /// <summary>Up to 50 characters of text immediately before the match.</summary>
    public string ContextBefore { get; set; } = string.Empty;

    /// <summary>Up to 50 characters of text immediately after the match.</summary>
    public string ContextAfter { get; set; } = string.Empty;
}
