namespace PropelIQ.Modules.SharedServices.Application.AI;

/// <summary>
/// Encrypted Redis store for per-request PII redaction token maps (US_054, AC-3).
///
/// The map is serialised to JSON, encrypted with AES-256-GCM, and stored under
/// key <c>redaction:{correlationId}</c> with a 5-minute TTL.  This is sufficient for
/// a synchronous AI round-trip and automatically evicts stale maps so raw PII values
/// do not linger in cache beyond the session window.
/// </summary>
public interface IRedactionMapStore
{
    /// <summary>
    /// Encrypts and stores <paramref name="tokenMap"/> under the correlation ID.
    /// The TTL is fixed at 5 minutes (sufficient for synchronous AI round-trips).
    /// </summary>
    Task StoreAsync(
        Guid correlationId,
        Dictionary<string, string> tokenMap,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves and decrypts the token map for <paramref name="correlationId"/>.
    /// Returns <c>null</c> when the key is missing or expired — callers should return
    /// the response unchanged rather than failing.
    /// </summary>
    Task<Dictionary<string, string>?> GetAsync(
        Guid correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the token map from Redis after successful de-anonymization.
    /// TTL acts as a safety net; explicit deletion is the primary cleanup path.
    /// </summary>
    Task DeleteAsync(
        Guid correlationId,
        CancellationToken ct = default);
}
