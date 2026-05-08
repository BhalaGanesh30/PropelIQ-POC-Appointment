using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Conflict detection engine for drug-drug and drug-allergy interactions (FR-CA-003).
///
/// Algorithm:
/// 1. Load patient's medication and allergy facts via <see cref="IClinicalFactRepository"/>.
/// 2. Normalize all drug/allergy names using <see cref="INormalizationService"/>.
/// 3. Load active rules via <see cref="IConflictRuleRepository.GetActiveRulesAsync"/>.
/// 4. Drug-drug: cross-product of all medication pairs evaluated order-insensitively.
/// 5. Drug-allergy: cross-product of medications × allergies.
/// 6. Deduplication: group by (FactIdA, FactIdB); keep only highest-severity entry (Edge Case 2).
/// 7. Upsert <c>conflict_alerts</c> rows for each deduplicated match (idempotent).
/// 8. Staleness check: if MAX(last_updated_at) is older than the threshold, set RulesStale.
/// 9. Map and sort: Critical → High → Moderate → Low.
///
/// Results are cached by <see cref="IConflictCacheService"/> with a 30-second TTL (TR-004).
/// OpenTelemetry span emitted per call with patient.id, conflicts.total, conflicts.critical_count,
/// rules.stale tags, and a <c>conflict.detection.duration_ms</c> metric (OTel instrumentation).
/// </summary>
public sealed class ConflictDetectionService : IConflictDetectionService
{
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.ClinicalIntelligence.ConflictDetectionService");

    private static readonly Meter Meter =
        new("PropelIQ.ClinicalIntelligence");

    private static readonly Histogram<double> DetectionDuration =
        Meter.CreateHistogram<double>(
            "conflict.detection.duration_ms",
            unit: "ms",
            description: "Duration of the conflict detection pipeline in milliseconds.");

    /// <summary>Severity sort order: lower value = higher priority in output.</summary>
    private static readonly Dictionary<string, int> SeverityOrder =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["critical"] = 0,
            ["high"]     = 1,
            ["moderate"] = 2,
            ["low"]      = 3,
        };

    private readonly IClinicalFactRepository _factRepository;
    private readonly IConflictRuleRepository _ruleRepository;
    private readonly IConflictAlertRepository _alertRepository;
    private readonly IConflictCacheService _cache;
    private readonly INormalizationService _normalization;
    private readonly ILogger<ConflictDetectionService> _logger;
    private readonly TimeSpan _stalenessThreshold;

    public ConflictDetectionService(
        IClinicalFactRepository factRepository,
        IConflictRuleRepository ruleRepository,
        IConflictAlertRepository alertRepository,
        IConflictCacheService cache,
        INormalizationService normalization,
        IConfiguration configuration,
        ILogger<ConflictDetectionService> logger)
    {
        _factRepository  = factRepository;
        _ruleRepository  = ruleRepository;
        _alertRepository = alertRepository;
        _cache           = cache;
        _normalization   = normalization;
        _logger          = logger;

        // Default staleness threshold: 30 days. Configurable via "ConflictDetection:StalenessThresholdDays".
        var days = configuration.GetValue("ConflictDetection:StalenessThresholdDays", 30);
        _stalenessThreshold = TimeSpan.FromDays(days);
    }

    /// <inheritdoc />
    public async Task<ConflictAlertsResponseDto> EvaluateConflictsAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        // ── Cache read ──────────────────────────────────────────────────────────
        var cached = await _cache.GetAsync(patientId, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Conflict cache hit for patient {PatientId}", patientId);
            return cached;
        }

        // ── Detection pipeline ─────────────────────────────────────────────────
        using var activity = ActivitySource.StartActivity(
            "ConflictDetectionService.EvaluateConflictsAsync",
            ActivityKind.Internal);

        activity?.SetTag("patient.id", patientId);

        var sw = Stopwatch.StartNew();

        try
        {
            var response = await RunDetectionPipelineAsync(patientId, activity, ct);
            sw.Stop();

            DetectionDuration.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("patient.id", patientId));

            // ── Cache write ───────────────────────────────────────────────────
            await _cache.SetAsync(patientId, response, ct);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Conflict detection failed for patient {PatientId}", patientId);
            throw;
        }
    }

    // ── Private: detection pipeline ──────────────────────────────────────────

    private async Task<ConflictAlertsResponseDto> RunDetectionPipelineAsync(
        Guid patientId,
        Activity? activity,
        CancellationToken ct)
    {
        // Step 1 – Load facts and rules concurrently.
        var factsTask = _factRepository.GetByPatientIdAsync(patientId, ct);
        var rulesTask = _ruleRepository.GetActiveRulesAsync(ct);

        await Task.WhenAll(factsTask, rulesTask);

        var allFacts = factsTask.Result;
        var rules    = rulesTask.Result;

        // Step 2 – Partition by type.
        var medications = allFacts
            .Where(f => string.Equals(f.FactType, "medication", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allergies = allFacts
            .Where(f => string.Equals(f.FactType, "allergy", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Step 3 – Normalize names using NormalizationService (reuse US_044).
        var normalizedMeds     = medications.Select(f => (Fact: f, Name: NormalizeName(f, "medication"))).ToList();
        var normalizedAllergies = allergies.Select(f => (Fact: f, Name: NormalizeName(f, "allergy"))).ToList();

        // Step 4 – Evaluate drug-drug cross-product.
        var matches = new List<MatchedConflict>();

        for (var i = 0; i < normalizedMeds.Count; i++)
        {
            for (var j = i + 1; j < normalizedMeds.Count; j++)
            {
                var (factA, nameA) = normalizedMeds[i];
                var (factB, nameB) = normalizedMeds[j];

                var rule = FindRule(rules, "drug_drug", nameA, nameB);
                if (rule is not null)
                {
                    matches.Add(new MatchedConflict(factA.Id, factB.Id, rule, nameA, nameB));
                }
            }
        }

        // Step 5 – Evaluate drug-allergy cross-product.
        foreach (var (medFact, medName) in normalizedMeds)
        {
            foreach (var (allergyFact, allergyName) in normalizedAllergies)
            {
                var rule = FindRule(rules, "drug_allergy", medName, allergyName);
                if (rule is not null)
                {
                    matches.Add(new MatchedConflict(medFact.Id, allergyFact.Id, rule, medName, allergyName));
                }
            }
        }

        // Step 6 – Deduplication: keep highest severity per (FactIdA, FactIdB) pair (Edge Case 2).
        var deduplicated = matches
            .GroupBy(m => (MinId: MinGuid(m.FactIdA, m.FactIdB), MaxId: MaxGuid(m.FactIdA, m.FactIdB)))
            .Select(g => g.OrderBy(m => SeverityOrder.GetValueOrDefault(m.Rule.Severity, 99)).First())
            .ToList();

        // Step 7 – Upsert conflict_alerts rows (idempotent).
        var persistedAlerts = new List<ConflictAlert>(deduplicated.Count);
        foreach (var match in deduplicated)
        {
            var entity = new ConflictAlert
            {
                PatientId    = patientId,
                FactIdA      = match.FactIdA,
                FactIdB      = match.FactIdB,
                RuleId       = match.Rule.Id,
                ConflictType = match.Rule.RuleType,
                Severity     = match.Rule.Severity,
                Description  = match.Rule.Description,
                DrugA        = match.DrugA,
                DrugB        = match.DrugB,
            };
            persistedAlerts.Add(await _alertRepository.UpsertAsync(entity, ct));
        }

        // Also load any previously persisted alerts not detected this run (e.g. already acknowledged).
        var allPersistedAlerts = await _alertRepository.GetByPatientIdAsync(patientId, ct);

        // Step 8 – Staleness check (Edge Case 1).
        var lastUpdated  = await _ruleRepository.GetLastUpdatedAtAsync(ct);
        var rulesStale   = lastUpdated.HasValue
                        && (DateTimeOffset.UtcNow - lastUpdated.Value) > _stalenessThreshold;

        // Step 9 – Map to DTOs and sort Critical → High → Moderate → Low.
        var dtos = allPersistedAlerts
            .Select(MapToDto)
            .OrderBy(d => SeverityOrder.GetValueOrDefault(d.Severity, 99))
            .ToList();

        var criticalCount = dtos.Count(d => string.Equals(d.Severity, "critical", StringComparison.OrdinalIgnoreCase));

        activity?.SetTag("conflicts.total", dtos.Count);
        activity?.SetTag("conflicts.critical_count", criticalCount);
        activity?.SetTag("rules.stale", rulesStale);

        _logger.LogInformation(
            "Conflict detection complete for patient {PatientId}: {Total} alerts ({Critical} critical), RulesStale={Stale}",
            patientId, dtos.Count, criticalCount, rulesStale);

        return new ConflictAlertsResponseDto
        {
            Alerts    = dtos,
            RulesStale = rulesStale,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Normalizes a <see cref="ClinicalFact.Name"/> by mapping it through
    /// <see cref="INormalizationService"/> via a temporary <see cref="ExtractedFact"/>.
    /// Falls back to the raw Name (trimmed, lowercase) if null.
    /// </summary>
    private string NormalizeName(ClinicalFact fact, string factType)
    {
        var rawName = (fact.Name ?? fact.Value).Trim();

        // Wrap in a temporary ExtractedFact so we can reuse the existing normalization tables.
        var tempFact = new ExtractedFact(
            FactType:   factType,
            Name:       rawName,
            Value:      rawName,
            Confidence: 1.0m,
            SourceText: string.Empty);

        var normalized = _normalization.Normalize([tempFact]);
        return normalized.Count > 0
            ? normalized[0].Name.ToLowerInvariant().Trim()
            : rawName.ToLowerInvariant().Trim();
    }

    /// <summary>
    /// Finds the best matching rule for a given pair of drug names. Order-insensitive for drug_drug.
    /// </summary>
    private static ConflictRule? FindRule(
        IReadOnlyList<ConflictRule> rules,
        string ruleType,
        string nameA,
        string nameB)
    {
        return rules.FirstOrDefault(r =>
            string.Equals(r.RuleType, ruleType, StringComparison.OrdinalIgnoreCase)
            && (
                // Forward match
                (ContainsName(r.DrugAName, nameA) && ContainsName(r.DrugBName, nameB))
                // Reverse match — order-insensitive for drug-drug pairs
                || (string.Equals(ruleType, "drug_drug", StringComparison.OrdinalIgnoreCase)
                    && ContainsName(r.DrugAName, nameB) && ContainsName(r.DrugBName, nameA))
            ));
    }

    /// <summary>
    /// Returns true if the stored rule name is contained within (or equals) the candidate name,
    /// using case-insensitive comparison. This handles partial-name matches from normalization.
    /// </summary>
    private static bool ContainsName(string ruleName, string candidateName)
        => candidateName.Contains(ruleName.Trim(), StringComparison.OrdinalIgnoreCase)
        || ruleName.Contains(candidateName.Trim(), StringComparison.OrdinalIgnoreCase);

    private static ConflictAlertDto MapToDto(ConflictAlert a) => new()
    {
        ConflictId    = a.Id,
        ConflictType  = a.ConflictType,
        Severity      = a.Severity,
        Description   = a.Description,
        DrugA         = a.DrugA,
        DrugB         = a.DrugB,
        Acknowledged  = a.Acknowledged,
        AcknowledgedAt = a.AcknowledgedAt,
        AcknowledgedBy = a.AcknowledgedBy?.ToString(),
    };

    private static Guid MinGuid(Guid a, Guid b) => a.CompareTo(b) <= 0 ? a : b;
    private static Guid MaxGuid(Guid a, Guid b) => a.CompareTo(b) >= 0 ? a : b;

    // ── Private nested types ──────────────────────────────────────────────────

    private sealed record MatchedConflict(
        Guid FactIdA,
        Guid FactIdB,
        ConflictRule Rule,
        string DrugA,
        string DrugB);
}
