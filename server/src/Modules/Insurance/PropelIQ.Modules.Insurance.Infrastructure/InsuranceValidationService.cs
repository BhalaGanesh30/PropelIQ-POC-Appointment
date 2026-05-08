using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Insurance.Application.Abstractions;
using PropelIQ.Modules.Insurance.Application.Dto;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Insurance.Infrastructure;

/// <summary>
/// Implements insurance soft validation against a Redis-cached provider reference table
/// (EP-005 US_037 AC-1 to AC-4, Edge Cases 1–2).
///
/// Performance contract: provider list is cached in Redis under the key
/// <c>insurance:providers:all</c> with a 5-minute TTL, keeping every call well
/// within the 500ms p95 SLA (NFR-002) even when the primary DB is under load.
/// </summary>
public sealed class InsuranceValidationService : IInsuranceValidationService
{
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Insurance.InsuranceValidationService");

    private const string ProvidersCacheKey = "insurance:providers:all";
    private static readonly TimeSpan ProvidersCacheTtl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<InsuranceValidationService> _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public InsuranceValidationService(
        AppDbContext db,
        IDistributedCache cache,
        ILogger<InsuranceValidationService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InsuranceValidateResponse> ValidateAsync(
        InsuranceValidateRequest request,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("ValidateInsurance");
        activity?.SetTag("insurance.tier", request.Tier);
        activity?.SetTag("insurance.providerCode", request.ProviderCode);

        var warnings = new List<InsuranceValidationWarning>();
        bool providerMatch;
        bool policyFormatValid;
        ValidationStatus status;

        try
        {
            // ── Provider lookup (Redis cache-aside, 5-min TTL) ─────────────────
            var providers = await GetProvidersAsync(ct);
            var provider = providers.Find(p =>
                string.Equals(p.ProviderCode, request.ProviderCode,
                    StringComparison.OrdinalIgnoreCase));

            providerMatch = provider is not null;

            if (!providerMatch)
            {
                warnings.Add(new InsuranceValidationWarning
                {
                    Field = "providerCode",
                    Message = $"Provider code '{request.ProviderCode}' was not found in the reference database.",
                });
            }

            // ── Policy format validation ───────────────────────────────────────
            policyFormatValid = ValidatePolicyFormat(
                provider?.PolicyNumberPattern,
                request.PolicyNumber,
                warnings);

            // ── Duplicate policy number detection (Edge Case 2) ────────────────
            if (!string.IsNullOrWhiteSpace(request.PrimaryPolicyNumber) &&
                string.Equals(request.PrimaryPolicyNumber, request.PolicyNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(new InsuranceValidationWarning
                {
                    Field = string.Empty,
                    Message = "Secondary policy number matches primary — potential duplicate data entry error.",
                });
            }

            // ── Result categorisation ─────────────────────────────────────────
            status = CategoriseResult(providerMatch, policyFormatValid, warnings.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Edge Case 1: reference database unreachable — validation deferred.
            _logger.LogWarning(ex,
                "Insurance reference DB unavailable for PatientId={PatientId}. " +
                "Returning ValidationPending.",
                request.PatientId);

            status = ValidationStatus.ValidationPending;
            providerMatch = false;
            policyFormatValid = false;
            warnings.Add(new InsuranceValidationWarning
            {
                Field = string.Empty,
                Message = "Validation deferred — reference database unavailable. A background retry will be queued.",
            });
        }

        // ── Persist audit record ───────────────────────────────────────────────
        var resultId = await PersistValidationResultAsync(request, status, warnings, ct);

        return new InsuranceValidateResponse
        {
            Status = status,
            Warnings = warnings,
            ProviderMatch = providerMatch,
            PolicyFormatValid = policyFormatValid,
            ValidationResultId = resultId,
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<List<InsuranceProviderCacheEntry>> GetProvidersAsync(CancellationToken ct)
    {
        try
        {
            var cached = await _cache.GetStringAsync(ProvidersCacheKey, ct);
            if (cached is not null)
            {
                return JsonSerializer.Deserialize<List<InsuranceProviderCacheEntry>>(
                    cached, JsonOpts)
                    ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for key {Key}; falling back to DB.", ProvidersCacheKey);
        }

        var entities = await _db.Set<InsuranceProvider>()
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new InsuranceProviderCacheEntry
            {
                ProviderCode = p.ProviderCode,
                PolicyNumberPattern = p.PolicyNumberPattern,
            })
            .ToListAsync(ct);

        try
        {
            var json = JsonSerializer.Serialize(entities);
            await _cache.SetStringAsync(
                ProvidersCacheKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ProvidersCacheTtl,
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for key {Key}; cache miss on next call.", ProvidersCacheKey);
        }

        return entities;
    }

    private static bool ValidatePolicyFormat(
        string? pattern,
        string policyNumber,
        List<InsuranceValidationWarning> warnings)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true; // No format constraint for this provider.

        var isValid = Regex.IsMatch(
            policyNumber,
            pattern,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(100));

        if (!isValid)
        {
            warnings.Add(new InsuranceValidationWarning
            {
                Field = "policyNumber",
                Message = "Policy number format does not match the expected pattern for this provider.",
            });
        }

        return isValid;
    }

    private static ValidationStatus CategoriseResult(
        bool providerMatch,
        bool policyFormatValid,
        int warningCount)
    {
        if (!providerMatch && !policyFormatValid)
            return ValidationStatus.ValidationFailed;

        if (warningCount > 0)
            return ValidationStatus.Warning;

        return ValidationStatus.SoftValidated;
    }

    private async Task<Guid?> PersistValidationResultAsync(
        InsuranceValidateRequest request,
        ValidationStatus status,
        List<InsuranceValidationWarning> warnings,
        CancellationToken ct)
    {
        try
        {
            var record = new InsuranceValidationResult
            {
                PatientId = request.PatientId,
                PolicyNumber = request.PolicyNumber,
                ProviderCode = request.ProviderCode,
                Tier = request.Tier,
                Status = status.ToString(),
                WarningsJson = warnings.Count > 0
                    ? JsonSerializer.Serialize(warnings)
                    : null,
                RetryCount = 0,
            };

            _db.Set<InsuranceValidationResult>().Add(record);
            await _db.SaveChangesAsync(ct);
            return record.Id;
        }
        catch (Exception ex)
        {
            // Non-fatal: validation result already computed; just log and continue.
            _logger.LogError(ex,
                "Failed to persist InsuranceValidationResult for PatientId={PatientId}.",
                request.PatientId);
            return null;
        }
    }

    // ── Cache projection ───────────────────────────────────────────────────────

    private sealed class InsuranceProviderCacheEntry
    {
        public string ProviderCode { get; set; } = string.Empty;
        public string PolicyNumberPattern { get; set; } = string.Empty;
    }
}
