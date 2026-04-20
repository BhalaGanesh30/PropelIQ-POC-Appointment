using System.Security.Cryptography;
using System.Text;

namespace PropelIQ.SharedKernel.Observability;

/// <summary>
/// Hashes high-cardinality values before they are used as trace attributes or
/// metric label values, preventing cardinality explosion in the telemetry backend.
///
/// Edge case: Patient IDs, appointment IDs, and similar PII-adjacent identifiers
/// are SHA-256 hashed to a 16-character hex prefix so telemetry backends can
/// correlate spans without storing the raw identifier.
/// </summary>
public static class CardinalityHasher
{
    /// <summary>
    /// Returns a 16-character lowercase hex string derived from the SHA-256 hash
    /// of <paramref name="value"/>. Suitable for trace attribute values where
    /// uniqueness matters but the raw value must not be transmitted.
    /// </summary>
    /// <example>
    /// using var activity = DiagnosticsConfig.ActivitySource.StartActivity("GetPatient");
    /// activity?.SetTag("patient_id_hash", CardinalityHasher.HashForTrace(patientId));
    /// </example>
    public static string HashForTrace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        // Take the first 8 bytes (16 hex chars) — sufficient uniqueness for correlation.
        // BitConverter used instead of Convert.ToHexStringLower (requires .NET 9+).
        return BitConverter.ToString(bytes, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
    }
}
