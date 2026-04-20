namespace PropelIQ.SharedKernel.Caching;

/// <summary>
/// Generates type-safe, collision-free cache keys following the convention
/// <c>{Domain}:{EntityType}:{Identifier}</c>.
/// The configured <c>InstanceName</c> ("PropelIQ:") is prepended automatically
/// by StackExchange.Redis, so it is omitted here.
/// Examples:
///   Scheduling:SlotSearch:2026-04-16:morning
///   Scheduling:Slot:d4c7a3b2-...
/// </summary>
public static class CacheKeyBuilder
{
    /// <summary>Builds a full cache key: <c>{domain}:{entityType}:{identifier}</c>.</summary>
    public static string Build(string domain, string entityType, string identifier)
        => $"{domain}:{entityType}:{identifier}";

    /// <summary>Builds a key with a compound identifier (e.g. date + shift).</summary>
    public static string Build(string domain, string entityType, params string[] identifierParts)
        => $"{domain}:{entityType}:{string.Join(':', identifierParts)}";

    /// <summary>
    /// Builds a prefix used for scan-based prefix invalidation:
    /// <c>{domain}:{entityType}:</c>.
    /// </summary>
    public static string BuildPrefix(string domain, string entityType)
        => $"{domain}:{entityType}:";
}
