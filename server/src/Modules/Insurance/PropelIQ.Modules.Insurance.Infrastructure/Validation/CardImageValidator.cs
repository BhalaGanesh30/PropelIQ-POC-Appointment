namespace PropelIQ.Modules.Insurance.Infrastructure.Validation;

/// <summary>
/// Result returned by <see cref="CardImageValidator.Validate"/>.
/// </summary>
public sealed class CardImageValidationResult
{
    public bool IsValid { get; init; }

    /// <summary>Human-readable error message; null when <see cref="IsValid"/> is true.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// Detected file extension in lower-case (<c>jpg</c>, <c>png</c>, <c>pdf</c>).
    /// Null when <see cref="IsValid"/> is false.
    /// </summary>
    public string? DetectedExtension { get; init; }
}

/// <summary>
/// Magic-byte file type validation for insurance card images (EP-005 US_038 AC-3).
///
/// Accepted types: JPEG, PNG, PDF.  Maximum size: 10 MB.
///
/// Validation is performed on the raw file bytes using magic byte signatures — NOT
/// on the file extension or <c>Content-Type</c> header — to prevent content-type
/// spoofing attacks (OWASP A01 / File Upload Security).
///
/// Magic byte references:
///   JPEG  — <c>FF D8 FF</c> (first 3 bytes)
///   PNG   — <c>89 50 4E 47</c> (first 4 bytes, = <c>.PNG</c>)
///   PDF   — <c>25 50 44 46</c> (first 4 bytes, = <c>%PDF</c>)
/// </summary>
public static class CardImageValidator
{
    /// <summary>Maximum permitted file size in bytes (10 MB).</summary>
    public const long MaxFileSizeBytes = 10L * 1024 * 1024;

    /// <summary>
    /// Validates the file declared size and its magic bytes.
    /// The caller is responsible for buffering the file content before calling this
    /// method; pass the first ≥ 8 bytes of the file in <paramref name="header"/>.
    /// </summary>
    /// <param name="fileLength">Total declared file size in bytes.</param>
    /// <param name="header">
    /// Buffer containing at least the first 8 bytes of the file content.
    /// If the file is shorter than 8 bytes, pass as many bytes as exist.
    /// </param>
    public static CardImageValidationResult Validate(long fileLength, ReadOnlySpan<byte> header)
    {
        if (fileLength <= 0)
            return new CardImageValidationResult
            {
                IsValid = false,
                Error = "File is empty.",
            };

        if (fileLength > MaxFileSizeBytes)
            return new CardImageValidationResult
            {
                IsValid = false,
                Error = $"File exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.",
            };

        if (IsJpeg(header))
            return new CardImageValidationResult { IsValid = true, DetectedExtension = "jpg" };

        if (IsPng(header))
            return new CardImageValidationResult { IsValid = true, DetectedExtension = "png" };

        if (IsPdf(header))
            return new CardImageValidationResult { IsValid = true, DetectedExtension = "pdf" };

        return new CardImageValidationResult
        {
            IsValid = false,
            Error = "File type not supported. Accepted types: JPEG, PNG, PDF. " +
                    "Ensure the file is not renamed from a different format.",
        };
    }

    // ── Magic byte matchers ────────────────────────────────────────────────────

    // JPEG: FF D8 FF (SOI marker)
    private static bool IsJpeg(ReadOnlySpan<byte> b) =>
        b.Length >= 3 &&
        b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    // PNG: 89 50 4E 47 (= .PNG)
    private static bool IsPng(ReadOnlySpan<byte> b) =>
        b.Length >= 4 &&
        b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;

    // PDF: 25 50 44 46 (= %PDF)
    private static bool IsPdf(ReadOnlySpan<byte> b) =>
        b.Length >= 4 &&
        b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;
}
