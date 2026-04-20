# Task - TASK_002

## Requirement Reference

- User Story: us_004
- Story Location: .propel/context/tasks/EP-TECH/us_004/us_004.md
- Acceptance Criteria:
  - AC-2: Given the API is running with Redis connected, When a slot search result is cached with a configured TTL, Then a second identical request within TTL returns the cached response without hitting the database.
  - AC-3: Given Redis is configured in the API, When the TTL expires on a cached entry, Then the entry is evicted and the next request fetches fresh data from the database.
  - AC-4: Given Redis becomes unavailable, When a request requires a cached value, Then the system gracefully falls back to the database and logs a warning without throwing an unhandled exception.
- Edge Case:
  - What happens if Redis connection times out? Circuit breaker activates; requests fall through to the database with cache miss logged.
  - How does the system handle cache invalidation on booking confirmation? Slot reservation invalidates the affected slot's cache key immediately.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | ASP.NET Core Web API | 8.x |
| Database | N/A | N/A |
| Library | Microsoft.Extensions.Caching.StackExchangeRedis | 8.x |
| Library | StackExchange.Redis | 2.x |
| Library | Microsoft.Extensions.Http.Resilience | 8.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Integrate Redis as a distributed cache in the ASP.NET Core 8 API using `IDistributedCache` with StackExchange.Redis. Implement a cache service abstraction with configurable TTL controls, a circuit breaker pattern for graceful fallback when Redis is unavailable, explicit cache invalidation support for booking-related keys, and structured warning logging on cache failures. This task delivers the caching acceleration layer required by TR-004 to meet NFR-001 (3s p95 page-load) and NFR-002 (500ms p95 API response) SLOs for slot search and profile read endpoints.

## Dependent Tasks

- task_001_infra_redis_provisioning (requires running Redis instance in Docker Compose)
- US_002 task_001_be_aspnet_solution_scaffold (requires compiled ASP.NET Core solution)
- US_002 task_002_be_api_middleware_and_health (requires middleware pipeline and health check infrastructure)

## Impacted Components

- Modified: `server/src/PropelIQ.Api/PropelIQ.Api.csproj` (Redis caching NuGet packages)
- Modified: `server/src/PropelIQ.Api/Program.cs` (Redis distributed cache and health check registration)
- New: `server/src/PropelIQ.SharedKernel/Caching/ICacheService.cs` (cache service abstraction interface)
- New: `server/src/PropelIQ.SharedKernel/Caching/CacheKeyBuilder.cs` (type-safe cache key generation)
- New: `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Caching/RedisCacheService.cs` (IDistributedCache wrapper with circuit breaker)
- New: `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Caching/CacheOptions.cs` (TTL configuration model)
- Modified: `server/src/PropelIQ.Api/appsettings.json` (cache TTL configuration section)

## Implementation Plan

1. **Install NuGet packages** in the API project:
   - `Microsoft.Extensions.Caching.StackExchangeRedis` 8.x (Redis distributed cache provider)
   - `Microsoft.Extensions.Resilience` 8.x (for Polly-based circuit breaker)

2. **Register Redis distributed cache** in `Program.cs` using `AddStackExchangeRedisCache`:

### ASP.NET Core 8 Redis Cache Registration Reference

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration
        .GetConnectionString("Redis");
    options.InstanceName = "PropelIQ:";
});
```

Source: ASP.NET Core 8.0.21 AddStackExchangeRedisCache documentation

3. **Create cache service abstraction** (`ICacheService`) in SharedKernel with methods for typed get/set/remove operations and TTL configuration:

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null,
        CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
```

4. **Implement `RedisCacheService`** wrapping `IDistributedCache` with:
   - JSON serialization using `System.Text.Json` for typed cache entries
   - Configurable default and per-domain TTL values from `appsettings.json`
   - Circuit breaker pattern using `Microsoft.Extensions.Resilience` / Polly:
     - Break after 3 consecutive Redis exceptions within 30 seconds
     - Half-open after 60 seconds to retry Redis connectivity
     - When circuit is open, all cache reads return `null` (cache miss) and cache writes are silently skipped
   - Structured warning logging on every cache failure via `ILogger<RedisCacheService>`
   - No unhandled exceptions propagate to callers

### Circuit Breaker Fallback Pattern

```csharp
public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
{
    try
    {
        var cached = await _distributedCache.GetStringAsync(key, ct);
        if (cached is null) return default;
        return JsonSerializer.Deserialize<T>(cached);
    }
    catch (RedisConnectionException ex)
    {
        _logger.LogWarning(ex,
            "Redis unavailable for key {CacheKey}. Falling back to database.",
            key);
        return default;
    }
}
```

5. **Create `CacheKeyBuilder`** for type-safe, collision-free key generation following the convention `{InstanceName}:{Domain}:{EntityType}:{Identifier}`:
   - Example: `PropelIQ:Scheduling:SlotSearch:2026-04-16:morning`
   - Example: `PropelIQ:Scheduling:Slot:{slotId}`

6. **Configure TTL settings** in `appsettings.json` with per-domain defaults:

```json
{
  "CacheSettings": {
    "DefaultTtlSeconds": 300,
    "SlotSearchTtlSeconds": 120,
    "ProfileReadTtlSeconds": 600,
    "CircuitBreaker": {
      "FailureThreshold": 3,
      "BreakDurationSeconds": 60,
      "SamplingWindowSeconds": 30
    }
  }
}
```

7. **Add Redis health check** to the existing health check pipeline from US_002/task_002. Register `AddRedis()` health check from `AspNetCore.HealthChecks.Redis` package (or use StackExchange.Redis connectivity check) with `Degraded` failure status.

8. **Implement cache invalidation pattern** for booking-related keys. Create an `ICacheInvalidator` interface with a method `InvalidateSlotCacheAsync(Guid slotId)` that removes the specific slot key and the related slot search keys by prefix. This is wired into booking confirmation flows in later epics (EP-002).

## Current Project State

```text
server/
├── PropelIQ.sln
├── src/
│   ├── PropelIQ.Api/
│   │   ├── PropelIQ.Api.csproj
│   │   ├── Program.cs           (middleware pipeline from US_002)
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   ├── PropelIQ.SharedKernel/
│   │   ├── PropelIQ.SharedKernel.csproj
│   │   └── BaseEntity.cs
│   └── Modules/
│       └── SharedServices/
│           └── PropelIQ.Modules.SharedServices.Infrastructure/
docker-compose.yml   (PostgreSQL + Redis from task_001)
.env.example
```

> Assumes US_002 tasks and US_004/task_001 are completed. Update on execution if structure differs.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | server/src/PropelIQ.Api/PropelIQ.Api.csproj | Add Microsoft.Extensions.Caching.StackExchangeRedis and Microsoft.Extensions.Resilience packages |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register AddStackExchangeRedisCache, ICacheService, and Redis health check |
| CREATE | server/src/PropelIQ.SharedKernel/Caching/ICacheService.cs | Cache service abstraction with typed get/set/remove and TTL |
| CREATE | server/src/PropelIQ.SharedKernel/Caching/ICacheInvalidator.cs | Cache invalidation interface for booking-related key removal |
| CREATE | server/src/PropelIQ.SharedKernel/Caching/CacheKeyBuilder.cs | Type-safe cache key generation utility |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Caching/RedisCacheService.cs | IDistributedCache wrapper with circuit breaker and structured logging |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Caching/CacheOptions.cs | TTL and circuit breaker configuration POCO |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Caching/SlotCacheInvalidator.cs | ICacheInvalidator implementation for slot-related keys |
| MODIFY | server/src/PropelIQ.Api/appsettings.json | Add CacheSettings section with TTL and circuit breaker config |

## External References

- ASP.NET Core 8 distributed caching with Redis: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0
- StackExchange.Redis configuration: https://stackexchange.github.io/StackExchange.Redis/Configuration
- Microsoft.Extensions.Resilience (Polly integration): https://learn.microsoft.com/en-us/dotnet/core/resilience/?tabs=dotnet-cli
- IDistributedCache interface: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache
- DistributedCacheEntryOptions (TTL): https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.distributedcacheentryoptions
- Redis health checks for ASP.NET Core: https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks

## Build Commands

```bash
# Build solution
dotnet build server/PropelIQ.sln

# Start all services (PostgreSQL + Redis)
docker compose up -d

# Run API
dotnet run --project server/src/PropelIQ.Api/PropelIQ.Api.csproj

# Verify health endpoint includes Redis check
curl http://localhost:5000/api/v1/health
```

## Implementation Validation Strategy

- [ ] `dotnet build server/PropelIQ.sln` compiles with zero errors after package additions
- [ ] `AddStackExchangeRedisCache` registers without startup errors when Redis is available
- [ ] Cached entry is returned on second identical request without database query (verify via logs)
- [ ] Cached entry is evicted after configured TTL expires; next request hits database
- [ ] When Redis is stopped (`docker compose stop redis`), API continues serving from database with warning logged
- [ ] Circuit breaker opens after 3 consecutive Redis failures; requests fall through to database
- [ ] Circuit breaker half-opens after 60 seconds and retries Redis
- [ ] `RemoveAsync` and `RemoveByPrefixAsync` correctly invalidate cache entries
- [ ] Health check reports `Degraded` when Redis is unavailable

## Implementation Checklist

- [x] Install `Microsoft.Extensions.Caching.StackExchangeRedis` 8.x and `Microsoft.Extensions.Resilience` 8.x NuGet packages
- [x] Register `AddStackExchangeRedisCache` with connection string and instance name in `Program.cs`
- [x] Create `ICacheService` interface in SharedKernel with typed get/set/remove methods
- [x] Implement `RedisCacheService` wrapping `IDistributedCache` with JSON serialization, circuit breaker, and structured warning logging
- [x] Create `CacheKeyBuilder` for type-safe key generation with `{Domain}:{EntityType}:{Identifier}` convention
- [x] Configure `CacheSettings` section in `appsettings.json` with per-domain TTL values and circuit breaker thresholds
- [x] Add Redis health check to existing health check pipeline with `Degraded` failure status
- [x] Create `ICacheInvalidator` and `SlotCacheInvalidator` for booking-related cache key removal
