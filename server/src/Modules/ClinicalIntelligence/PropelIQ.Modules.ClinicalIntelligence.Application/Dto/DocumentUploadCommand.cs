namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Service-level command carrying all data needed to process a document upload.
/// Constructed by the controller from the incoming multipart/form-data request;
/// avoids an <c>IFormFile</c> dependency in the Application layer.
/// </summary>
public sealed record DocumentUploadCommand(
    Stream FileContent,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Guid PatientId);
