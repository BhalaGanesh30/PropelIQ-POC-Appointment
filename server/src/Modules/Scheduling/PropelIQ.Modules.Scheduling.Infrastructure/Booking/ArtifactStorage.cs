using Microsoft.Extensions.Configuration;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// File-system implementation of <see cref="IArtifactStorage"/>.
/// Stores artifacts under a configurable base directory.
/// Replace with blob storage (Azure, S3) by swapping this registration.
///
/// NFR-010: storage paths are written back to the Appointment entity for audit.
/// </summary>
public sealed class ArtifactStorage : IArtifactStorage
{
    private readonly string _basePath;

    public ArtifactStorage(IConfiguration configuration)
    {
        _basePath = configuration["Storage:ArtifactBasePath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts");

        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> StoreAsync(
        string containerPath,
        string fileName,
        byte[] content,
        string contentType,
        CancellationToken ct)
    {
        var directory = Path.Combine(_basePath, containerPath);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(filePath, content, ct);

        // Return relative path so it is storage-agnostic (no absolute paths in DB).
        return Path.Combine(containerPath, fileName).Replace('\\', '/');
    }

    public async Task<byte[]?> RetrieveAsync(string storagePath, CancellationToken ct)
    {
        var fullPath = Path.Combine(_basePath, storagePath);
        if (!File.Exists(fullPath))
            return null;

        return await File.ReadAllBytesAsync(fullPath, ct);
    }
}
