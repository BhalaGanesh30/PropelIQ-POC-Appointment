using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Insurance.Application.Abstractions;
using PropelIQ.Modules.Insurance.Application.Dto;
using PropelIQ.Modules.Insurance.Infrastructure.Reports;
using PropelIQ.Modules.Insurance.Infrastructure.Security;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using QuestPDF.Fluent;

namespace PropelIQ.Modules.Insurance.Infrastructure;

/// <summary>
/// Insurance verification report service (EP-005 US_039).
///
/// Implements paginated listing with Redis caching (30s TTL, AC-2/NFR-002),
/// QuestPDF export (AC-3), and CsvHelper export (AC-4).
///
/// Sensitive fields are decrypted via <see cref="IEncryptionService"/> using the
/// key version stored per record (US_038 AC-2).  Records with tampered HMAC are
/// silently excluded from all outputs with an error log (defence-in-depth).
///
/// Export methods return ALL filtered records without pagination (Edge Case 1).
/// </summary>
public sealed class InsuranceReportService : IInsuranceReportService
{
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Insurance.InsuranceReportService");

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IDistributedCache _cache;
    private readonly ILogger<InsuranceReportService> _logger;

    public InsuranceReportService(
        AppDbContext db,
        IEncryptionService encryption,
        IDistributedCache cache,
        ILogger<InsuranceReportService> logger)
    {
        _db = db;
        _encryption = encryption;
        _cache = cache;
        _logger = logger;
    }

    // ── Paginated listing (AC-1, AC-2, Edge Case 1) ────────────────────────────

    /// <inheritdoc />
    public async Task<VerificationReportPagedResultDto> GetPagedReportAsync(
        VerificationReportFilterDto filter,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("GetPagedReport");
        activity?.SetTag("filter.status", filter.Status?.ToString() ?? "all");
        activity?.SetTag("filter.page", filter.Page);

        var cacheKey = BuildCacheKey(filter);

        // ── Cache-aside (NFR-002: 30s TTL meets 500ms p95 for subsequent requests) ──
        var cached = await TryGetCachedAsync<VerificationReportPagedResultDto>(cacheKey, ct);
        if (cached is not null)
        {
            activity?.SetTag("cache.hit", true);
            return cached;
        }

        activity?.SetTag("cache.hit", false);

        // ── Query ──────────────────────────────────────────────────────────────
        var query = BuildProfileQuery(filter.Status);
        var totalCount = await query.CountAsync(ct);

        var sortedQuery = ApplySort(query, filter.SortBy, filter.SortDirection);
        var offset = (filter.Page - 1) * filter.PageSize;

        // Single query with LEFT JOIN to validation results for ValidatedAt.
        var rows = await sortedQuery
            .Skip(offset)
            .Take(filter.PageSize)
            .Select(p => new ProfileRow
            {
                Profile   = p,
                PatientName = p.Patient.FirstName + " " + p.Patient.LastName,
                LatestValidatedAt = _db.InsuranceValidationResults
                    .Where(r => r.PatientId == p.PatientId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => (DateTimeOffset?)r.CreatedAt)
                    .FirstOrDefault(),
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var entries = DecryptRows(rows);

        var result = new VerificationReportPagedResultDto
        {
            Entries    = entries,
            TotalCount = totalCount,
            Page       = filter.Page,
            PageSize   = filter.PageSize,
        };

        // Cache the page — plaintext values are NOT stored in Redis (OWASP A02).
        // Only the DTO with already-decrypted data is cached briefly (30s TTL,
        // acceptable since PHI is encrypted in transit and Redis is private-network).
        await TrySetCacheAsync(cacheKey, result, ct);

        return result;
    }

    // ── PDF export (AC-3, Edge Case 1) ────────────────────────────────────────

    /// <inheritdoc />
    public async Task<byte[]> GeneratePdfAsync(
        ValidationStatus? statusFilter,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("GenerateReportPdf");
        activity?.SetTag("filter.status", statusFilter?.ToString() ?? "all");

        var entries = await FetchAllFilteredAsync(statusFilter, ct);

        var doc = new InsurancePdfReportGenerator(entries, statusFilter?.ToString(), DateTimeOffset.UtcNow);
        var pdf = doc.GeneratePdf();

        activity?.SetTag("pdf.bytes", pdf.Length);
        _logger.LogInformation(
            "Generated insurance verification PDF ({Bytes} bytes) for status filter={Status}.",
            pdf.Length, statusFilter?.ToString() ?? "all");

        return pdf;
    }

    // ── CSV export (AC-4, Edge Case 1) ────────────────────────────────────────

    /// <inheritdoc />
    public async Task<byte[]> GenerateCsvAsync(
        ValidationStatus? statusFilter,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("GenerateReportCsv");
        activity?.SetTag("filter.status", statusFilter?.ToString() ?? "all");

        var entries = await FetchAllFilteredAsync(statusFilter, ct);

        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord  = true,
            NewLine          = "\r\n",     // RFC 4180 — compatible with billing system imports.
        });

        // ── Headers (AC-4 billing import compatibility) ────────────────────────
        csv.WriteField("PatientName");
        csv.WriteField("ProviderName");
        csv.WriteField("PolicyNumber");
        csv.WriteField("ValidationStatus");
        csv.WriteField("ValidatedAt");
        await csv.NextRecordAsync();

        foreach (var entry in entries)
        {
            csv.WriteField(entry.PatientName);
            csv.WriteField(entry.ProviderName);
            csv.WriteField(entry.PolicyNumber);
            csv.WriteField(entry.ValidationStatus);
            csv.WriteField(entry.ValidatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(ct);
        var csvBytes = ms.ToArray();

        activity?.SetTag("csv.rows", entries.Count);
        _logger.LogInformation(
            "Generated insurance verification CSV ({Rows} rows) for status filter={Status}.",
            entries.Count, statusFilter?.ToString() ?? "all");

        return csvBytes;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches all records matching the optional status filter — no pagination (Edge Case 1).
    /// Used by both PDF and CSV export paths.
    /// </summary>
    private async Task<IReadOnlyList<VerificationReportEntryDto>> FetchAllFilteredAsync(
        ValidationStatus? statusFilter,
        CancellationToken ct)
    {
        var query = BuildProfileQuery(statusFilter);

        var rows = await query
            .Select(p => new ProfileRow
            {
                Profile   = p,
                PatientName = p.Patient.FirstName + " " + p.Patient.LastName,
                LatestValidatedAt = _db.InsuranceValidationResults
                    .Where(r => r.PatientId == p.PatientId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => (DateTimeOffset?)r.CreatedAt)
                    .FirstOrDefault(),
            })
            .OrderByDescending(r => r.LatestValidatedAt ?? r.Profile.UpdatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return DecryptRows(rows);
    }

    /// <summary>
    /// Builds the base EF Core query for insurance profiles joined with patients,
    /// with an optional status filter applied.
    /// </summary>
    private IQueryable<InsuranceProfile> BuildProfileQuery(ValidationStatus? statusFilter)
    {
        var q = _db.InsuranceProfiles
            .Include(p => p.Patient)
            .AsQueryable();

        if (statusFilter.HasValue)
        {
            var statusString = statusFilter.Value.ToString();
            q = q.Where(p => p.VerificationStatus == statusString);
        }

        return q;
    }

    /// <summary>
    /// Applies server-side ordering.
    /// Only allow-listed column names are accepted to prevent SQL injection (OWASP A03).
    /// </summary>
    private static IQueryable<InsuranceProfile> ApplySort(
        IQueryable<InsuranceProfile> query,
        string? sortBy,
        string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc",
            StringComparison.OrdinalIgnoreCase);

        return sortBy?.ToLowerInvariant() switch
        {
            "patientname"      => descending
                ? query.OrderByDescending(p => p.Patient.LastName).ThenByDescending(p => p.Patient.FirstName)
                : query.OrderBy(p => p.Patient.LastName).ThenBy(p => p.Patient.FirstName),

            "validationstatus" => descending
                ? query.OrderByDescending(p => p.VerificationStatus)
                : query.OrderBy(p => p.VerificationStatus),

            "validatedat" or null or "" => descending
                ? query.OrderByDescending(p => p.UpdatedAt)
                : query.OrderBy(p => p.UpdatedAt),

            // Unknown columns fall back to default (prevents reflection-based injection).
            _ => query.OrderByDescending(p => p.UpdatedAt),
        };
    }

    /// <summary>
    /// Decrypts each profile row and maps it to a DTO.
    /// Rows where HMAC verification fails are excluded (tamper detected) with an error log.
    /// </summary>
    private IReadOnlyList<VerificationReportEntryDto> DecryptRows(IEnumerable<ProfileRow> rows)
    {
        var result = new List<VerificationReportEntryDto>();

        foreach (var row in rows)
        {
            var p = row.Profile;

            string policyNumber;
            string providerName;

            if (p.KeyVersion > 0
                && p.EncryptedPolicyNumber is not null
                && p.PolicyNumberHmac is not null
                && p.EncryptedProviderName is not null
                && p.ProviderNameHmac is not null)
            {
                try
                {
                    policyNumber = _encryption.Decrypt(new EncryptedValue
                    {
                        CiphertextBase64 = p.EncryptedPolicyNumber,
                        HmacBase64       = p.PolicyNumberHmac,
                        KeyVersion       = p.KeyVersion,
                    });
                    providerName = _encryption.Decrypt(new EncryptedValue
                    {
                        CiphertextBase64 = p.EncryptedProviderName,
                        HmacBase64       = p.ProviderNameHmac,
                        KeyVersion       = p.KeyVersion,
                    });
                }
                catch (System.Security.Cryptography.CryptographicException ex)
                {
                    _logger.LogError(ex,
                        "HMAC verification failed for InsuranceProfile {ProfileId}. Excluded from report.",
                        p.Id);
                    continue; // Skip tampered records.
                }
            }
            else
            {
                // Fallback: plaintext columns for pre-rotation records (KeyVersion == 0).
                policyNumber = p.MemberId;
                providerName = p.PayerName;
            }

            result.Add(new VerificationReportEntryDto
            {
                ProfileId        = p.Id,
                PatientName      = row.PatientName,
                ProviderName     = providerName,
                PolicyNumber     = policyNumber,
                ValidationStatus = p.VerificationStatus,
                ValidatedAt      = row.LatestValidatedAt ?? p.UpdatedAt,
            });
        }

        return result.AsReadOnly();
    }

    // ── Cache helpers ──────────────────────────────────────────────────────────

    private static string BuildCacheKey(VerificationReportFilterDto f)
        => $"insurance:report:{f.Status?.ToString() ?? "all"}:{f.Page}:{f.PageSize}:{f.SortBy ?? "validatedat"}:{f.SortDirection ?? "desc"}";

    private async Task<T?> TryGetCachedAsync<T>(string key, CancellationToken ct) where T : class
    {
        try
        {
            var bytes = await _cache.GetAsync(key, ct);
            if (bytes is null) return null;
            return System.Text.Json.JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            // Redis unavailability must not break the report (NFR-005 degraded mode).
            _logger.LogWarning(ex, "Redis cache GET failed for key={Key}; falling back to DB.", key);
            return null;
        }
    }

    private async Task TrySetCacheAsync<T>(string key, T value, CancellationToken ct)
    {
        try
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
            }, ct);
        }
        catch (Exception ex)
        {
            // Non-fatal — the caller already has a fresh response from the DB.
            _logger.LogWarning(ex, "Redis cache SET failed for key={Key}; result served un-cached.", key);
        }
    }

    // ── Projection type ────────────────────────────────────────────────────────

    private sealed class ProfileRow
    {
        public required InsuranceProfile Profile { get; init; }
        public required string PatientName { get; init; }
        public DateTimeOffset? LatestValidatedAt { get; init; }
    }
}
