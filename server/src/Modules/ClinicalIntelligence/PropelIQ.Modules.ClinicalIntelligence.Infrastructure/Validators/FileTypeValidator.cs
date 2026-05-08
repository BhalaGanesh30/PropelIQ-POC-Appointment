namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Validators;

/// <summary>
/// Magic-byte file type validator for clinical document uploads.
/// Reads the first 8 bytes of the stream to confirm the file signature
/// matches PDF, JPEG, PNG, or TIFF — rejecting anything that does not
/// match regardless of the client-supplied MIME type (OWASP A03).
/// </summary>
public static class FileTypeValidator
{
    private const int HeaderLength = 8;

    // Magic byte signatures
    private static readonly byte[] PdfSignature  = [0x25, 0x50, 0x44, 0x46];           // %PDF
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];                  // JPEG SOI + App marker
    private static readonly byte[] PngSignature  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] TiffLe        = [0x49, 0x49, 0x2A, 0x00];            // TIFF little-endian
    private static readonly byte[] TiffBe        = [0x4D, 0x4D, 0x00, 0x2A];            // TIFF big-endian

    /// <summary>
    /// Returns <c>true</c> when the stream starts with a recognised magic-byte
    /// sequence for PDF, JPEG, PNG, or TIFF.  Stream position is reset to its
    /// original value after the check.
    /// </summary>
    /// <param name="fileStream">Seekable, readable stream. Must be positioned at the start of the file data.</param>
    public static bool IsAllowedFileType(Stream fileStream)
    {
        var originalPosition = fileStream.Position;
        Span<byte> header = stackalloc byte[HeaderLength];

        int bytesRead = fileStream.Read(header);
        fileStream.Seek(originalPosition, SeekOrigin.Begin);

        if (bytesRead < 3)
            return false;

        return StartsWith(header, PdfSignature)
            || StartsWith(header, JpegSignature)
            || StartsWith(header, PngSignature)
            || StartsWith(header, TiffLe)
            || StartsWith(header, TiffBe);
    }

    private static bool StartsWith(ReadOnlySpan<byte> header, ReadOnlySpan<byte> signature) =>
        header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);
}
