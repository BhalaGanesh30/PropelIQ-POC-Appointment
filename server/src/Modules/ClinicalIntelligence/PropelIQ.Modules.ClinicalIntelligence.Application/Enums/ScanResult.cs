namespace PropelIQ.Modules.ClinicalIntelligence.Application.Enums;

/// <summary>
/// Result of a ClamAV malware scan operation.
/// Used internally by <see cref="IMalwareScanService"/> and persisted to
/// <c>clinical_documents.scan_result</c> as a string value.
/// </summary>
public enum ScanResult
{
    /// <summary>No threats detected; file is safe to store.</summary>
    Clean,

    /// <summary>A malware signature was found; file must be rejected (AC-3).</summary>
    ThreatDetected,

    /// <summary>
    /// File is awaiting a scan — either ClamAV was unreachable (Edge Case 1) or
    /// the retry worker has not yet processed the document.
    /// </summary>
    PendingScan,

    /// <summary>
    /// ClamAV daemon could not be reached during this attempt.
    /// Callers should quarantine the file and schedule a retry.
    /// </summary>
    ScannerUnavailable,
}
