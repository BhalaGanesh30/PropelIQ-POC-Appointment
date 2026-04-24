namespace PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;

/// <summary>
/// Blob / file-system storage abstraction for confirmation artifacts (PDF, QR, ICS).
/// Implementations handle the physical persistence; tests can stub this interface.
/// </summary>
public interface IArtifactStorage
{
    /// <summary>
    /// Persist raw bytes under <paramref name="containerPath"/>/<paramref name="fileName"/>
    /// and return the relative storage path for later retrieval.
    /// </summary>
    Task<string> StoreAsync(
        string containerPath,
        string fileName,
        byte[] content,
        string contentType,
        CancellationToken ct);

    /// <summary>
    /// Retrieve the bytes at <paramref name="storagePath"/>.
    /// Returns <see langword="null"/> when the artifact has not yet been generated.
    /// </summary>
    Task<byte[]?> RetrieveAsync(string storagePath, CancellationToken ct);
}
