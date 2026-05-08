using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Insurance.Application.Dto;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Insurance.Infrastructure;

/// <summary>
/// Background service that periodically retries insurance records with
/// <c>ValidationPending</c> status (EP-005 US_037 Edge Case 1).
///
/// Execution model:
/// - Runs every 5 minutes.
/// - Queries <c>insurance_validation_results</c> for rows where
///   <c>status = 'ValidationPending'</c> AND <c>retry_count &lt; 3</c>.
/// - Re-runs format and provider-code validation using the cached provider list.
/// - Updates status to <c>SoftValidated</c>, <c>Warning</c>, or <c>ValidationFailed</c>
///   on success; increments <c>retry_count</c> and leaves status as
///   <c>ValidationPending</c> on continued failure (until retry_count reaches 3).
/// - Uses <see cref="IServiceScopeFactory"/> so the scoped <see cref="AppDbContext"/>
///   is resolved fresh each tick from a singleton host.
/// </summary>
public sealed class InsuranceValidationRetryService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int MaxRetries = 3;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InsuranceValidationRetryService> _logger;

    public InsuranceValidationRetryService(
        IServiceScopeFactory scopeFactory,
        ILogger<InsuranceValidationRetryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Insurance validation retry service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = await db.Set<InsuranceValidationResult>()
                .Where(r => r.Status == nameof(ValidationStatus.ValidationPending)
                         && r.RetryCount < MaxRetries)
                .ToListAsync(ct);

            if (pending.Count == 0)
                return;

            _logger.LogInformation(
                "Insurance retry service: processing {Count} pending record(s).", pending.Count);

            var providers = await db.Set<InsuranceProvider>()
                .AsNoTracking()
                .Where(p => p.IsActive)
                .ToListAsync(ct);

            foreach (var record in pending)
            {
                try
                {
                    var (newStatus, warnings) = RevalidateRecord(record, providers);
                    record.Status = newStatus.ToString();
                    record.WarningsJson = warnings.Count > 0
                        ? JsonSerializer.Serialize(warnings)
                        : null;

                    if (newStatus == ValidationStatus.ValidationPending)
                        record.RetryCount += 1;

                    _logger.LogInformation(
                        "Retry {Attempt} for InsuranceValidationResult {Id}: new status = {Status}.",
                        record.RetryCount, record.Id, newStatus);
                }
                catch (Exception ex)
                {
                    record.RetryCount += 1;
                    _logger.LogWarning(ex,
                        "Retry {Attempt} failed for InsuranceValidationResult {Id}.",
                        record.RetryCount, record.Id);
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Insurance validation retry service encountered an error.");
        }
    }

    // ── Inline re-validation (mirrors InsuranceValidationService logic) ────────

    private static (ValidationStatus status, List<InsuranceValidationWarning> warnings)
        RevalidateRecord(InsuranceValidationResult record, List<InsuranceProvider> providers)
    {
        var warnings = new List<InsuranceValidationWarning>();

        var provider = providers.Find(p =>
            string.Equals(p.ProviderCode, record.ProviderCode,
                StringComparison.OrdinalIgnoreCase));

        var providerMatch = provider is not null;
        if (!providerMatch)
        {
            warnings.Add(new InsuranceValidationWarning
            {
                Field = "providerCode",
                Message = $"Provider code '{record.ProviderCode}' was not found in the reference database.",
            });
        }

        var policyFormatValid = true;
        if (provider is not null && !string.IsNullOrWhiteSpace(provider.PolicyNumberPattern))
        {
            policyFormatValid = Regex.IsMatch(
                record.PolicyNumber,
                provider.PolicyNumberPattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(100));

            if (!policyFormatValid)
            {
                warnings.Add(new InsuranceValidationWarning
                {
                    Field = "policyNumber",
                    Message = "Policy number format does not match the expected pattern for this provider.",
                });
            }
        }

        var status = (!providerMatch && !policyFormatValid)
            ? ValidationStatus.ValidationFailed
            : warnings.Count > 0
                ? ValidationStatus.Warning
                : ValidationStatus.SoftValidated;

        return (status, warnings);
    }
}
