namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>Upload endpoint response (US_040 AC-2, AC-3, Edge Case 1).</summary>
public sealed class DocumentUploadResponse
{
    public Guid DocumentId { get; set; }
    public string ScanResult { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
