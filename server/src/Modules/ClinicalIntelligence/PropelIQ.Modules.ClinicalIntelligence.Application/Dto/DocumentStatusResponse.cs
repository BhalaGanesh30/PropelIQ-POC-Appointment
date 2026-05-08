namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Status polling response returned by <c>GET /api/v1/documents/{id}/status</c>.
/// Allows the frontend to track scan and extraction progress (US_040 task_001 polling).
/// </summary>
public sealed class DocumentStatusResponse
{
    public Guid DocumentId { get; set; }
    public string ScanResult { get; set; } = string.Empty;
    public string ExtractionStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
