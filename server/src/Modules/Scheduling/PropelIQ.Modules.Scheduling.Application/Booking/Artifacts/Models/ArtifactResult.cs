namespace PropelIQ.Modules.Scheduling.Application.Booking.Artifacts.Models;

/// <summary>
/// Result of the artifact generation pipeline for a single booking confirmation.
/// Paths are set per artifact; null indicates that generator failed or was skipped.
/// </summary>
public sealed class ArtifactResult
{
    public string? PdfPath { get; set; }
    public string? QrCodePath { get; set; }
    public string? IcsPath { get; set; }

    /// <summary>Raw bytes kept in memory for immediate email attachment (no re-read required).</summary>
    public byte[]? PdfBytes { get; set; }
    public byte[]? QrCodeBytes { get; set; }
    public byte[]? IcsBytes { get; set; }

    /// <summary><see langword="true"/> when all three artifacts were successfully generated.</summary>
    public bool AllGenerated =>
        PdfPath is not null && QrCodePath is not null && IcsPath is not null;
}
