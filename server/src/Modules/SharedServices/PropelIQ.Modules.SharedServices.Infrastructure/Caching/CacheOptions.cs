namespace PropelIQ.Modules.SharedServices.Infrastructure.Caching;

/// <summary>
/// TTL and circuit breaker configuration bound from the "CacheSettings" section
/// of appsettings.json. All values have safe defaults matching the task spec.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "CacheSettings";

    /// <summary>Default TTL for cache entries without a domain-specific override (seconds).</summary>
    public int DefaultTtlSeconds { get; init; } = 300;

    /// <summary>TTL for slot-search result sets (seconds). NFR-001: 3 s p95 page-load.</summary>
    public int SlotSearchTtlSeconds { get; init; } = 120;

    /// <summary>TTL for provider/patient profile reads (seconds). NFR-002: 500 ms p95 API.</summary>
    public int ProfileReadTtlSeconds { get; init; } = 600;

    /// <summary>Circuit breaker configuration thresholds.</summary>
    public CircuitBreakerOptions CircuitBreaker { get; init; } = new();
}

/// <summary>Polly circuit breaker thresholds for Redis resilience.</summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>Minimum number of Redis failures before the circuit trips open.</summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>Duration the circuit stays open before entering half-open state (seconds).</summary>
    public int BreakDurationSeconds { get; init; } = 60;

    /// <summary>Sliding window in which failures are counted (seconds).</summary>
    public int SamplingWindowSeconds { get; init; } = 30;
}
