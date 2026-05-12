using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.SharedServices.Application.AI;
using PropelIQ.Modules.SharedServices.Application.Audit;

namespace PropelIQ.Modules.SharedServices.Infrastructure.AI;

/// <summary>
/// Full PII redaction and de-anonymization pipeline for AI prompts (US_054, AC-1 through AC-3).
///
/// Implements <see cref="IPiiRedactionService"/> in two phases:
///
/// <b>Phase 1 — Pre-prompt (<see cref="RedactAsync"/>):</b>
/// <list type="number">
///   <item>Structured field scan: regex sweep for <c>field_name: value</c> patterns covering
///         each name in <see cref="PiiRedactionOptions.StructuredFields"/>.</item>
///   <item>NLP free-text scan: <see cref="NlpPiiDetector"/> returns scored matches;
///         only matches ≥ <see cref="PiiRedactionOptions.ConfidenceThreshold"/> are substituted;
///         below-threshold matches are logged as <c>pii_detection_low_confidence</c> (Edge Case 2).</item>
///   <item>Token map persisted to encrypted Redis via <see cref="IRedactionMapStore"/> (5-min TTL).</item>
///   <item><c>pii_redacted</c> audit event written via <see cref="IAuditService"/> (AC-2, NFR-010).</item>
/// </list>
///
/// Token format: <c>[REDACTED_{FIELD}_{hmachash}]</c> where <c>hmachash</c> is the first 8
/// characters of <c>HMAC-SHA256(value, hmacKey)</c> hex-encoded — deterministic per tenant key,
/// opaque, and reversible via the stored map (AC-1, AC-3).
///
/// <b>Phase 2 — Post-response (<see cref="DeAnonymizeAsync"/>):</b>
/// <list type="number">
///   <item>Token map retrieved and decrypted from Redis.</item>
///   <item>All <c>[REDACTED_*]</c> tokens replaced with original values.</item>
///   <item>Redis key deleted (explicit cleanup; TTL is safety net).</item>
///   <item><c>pii_deanonymized</c> audit event written (AC-2, AC-3, NFR-010).</item>
/// </list>
///
/// On any Phase 1 failure: <c>pii_redaction_failed</c> audit event is written and
/// <see cref="PiiRedactionFailureException"/> is thrown so callers can return a safe
/// fallback response without the prompt reaching the AI gateway (Edge Case 1).
/// </summary>
public sealed class PiiRedactionService : IPiiRedactionService
{
    private readonly NlpPiiDetector          _detector;
    private readonly IRedactionMapStore      _mapStore;
    private readonly IAuditService           _auditService;
    private readonly PiiRedactionOptions     _options;
    private readonly ILogger<PiiRedactionService> _logger;

    public PiiRedactionService(
        NlpPiiDetector detector,
        IRedactionMapStore mapStore,
        IAuditService auditService,
        IOptions<PiiRedactionOptions> options,
        ILogger<PiiRedactionService> logger)
    {
        _detector    = detector;
        _mapStore    = mapStore;
        _auditService = auditService;
        _options     = options.Value;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task<(string RedactedPrompt, RedactionContext Context)> RedactAsync(
        string prompt,
        Guid patientId,
        Guid clinicianId,
        CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();
        var ctx = new RedactionContext
        {
            CorrelationId = correlationId,
            PatientId     = patientId,
            ClinicianId   = clinicianId,
        };

        try
        {
            var redacted = prompt;

            // Phase 1a: Structured field redaction.
            // Matches:  patient_name: John Doe   |   "patient_name": "John Doe"
            foreach (var field in _options.StructuredFields)
            {
                var fieldPattern = BuildStructuredFieldPattern(field);
                redacted = fieldPattern.Replace(redacted, match =>
                {
                    var rawValue = match.Groups["value"].Value.Trim();
                    if (string.IsNullOrWhiteSpace(rawValue))
                        return match.Value;

                    var fieldTypeName = NormalizeFieldType(field);
                    var token         = GenerateToken(fieldTypeName, rawValue);

                    ctx.TokenMap[token] = rawValue;

                    // Replace just the value capture group inside the full match.
                    return match.Value.Replace(rawValue, token, StringComparison.Ordinal);
                });
            }

            // Phase 1b: NLP free-text redaction.
            // NlpPiiDetector returns matches pre-sorted descending by StartIndex so
            // right-to-left substitution avoids index drift.
            var nlpMatches = _detector.Detect(redacted);

            foreach (var match in nlpMatches)
            {
                if (match.Confidence >= _options.ConfidenceThreshold)
                {
                    var token = GenerateToken(match.FieldType, match.Value);
                    ctx.TokenMap[token] = match.Value;

                    // Right-to-left substitution: earlier positions are intact.
                    redacted = string.Concat(
                        redacted.AsSpan(0, match.StartIndex),
                        token,
                        redacted.AsSpan(match.StartIndex + match.Length));
                }
                else
                {
                    _logger.LogInformation(
                        "pii_detection_low_confidence: type={FieldType} confidence={Confidence:F2} " +
                        "threshold={Threshold:F2} — pattern matched but not substituted (Edge Case 2).",
                        match.FieldType, match.Confidence, _options.ConfidenceThreshold);
                }
            }

            // Phase 1c: Persist encrypted token map in Redis (AC-3).
            if (ctx.TokenMap.Count > 0)
            {
                await _mapStore.StoreAsync(correlationId, ctx.TokenMap, ct);
            }

            // Phase 1d: Audit log — field types only, no raw values (AC-2, NFR-010).
            await _auditService.LogEventAsync(
                eventType:        "pii_redacted",
                actorUserId:      clinicianId,
                targetEntityId:   patientId,
                targetEntityType: "Patient",
                metadata:         new Dictionary<string, string>
                {
                    ["correlation_id"]   = correlationId.ToString(),
                    ["fields_redacted"]  = string.Join(",", ctx.TokenMap.Keys
                                              .Select(ExtractFieldTypeFromToken)
                                              .Distinct()),
                    ["token_count"]      = ctx.TokenMap.Count.ToString(),
                    ["request_id"]       = correlationId.ToString(),
                },
                ct: ct);

            _logger.LogInformation(
                "PII redaction complete: correlation={CorrelationId} tokens={TokenCount} " +
                "patient={PatientId} (AC-1, AC-2).",
                correlationId, ctx.TokenMap.Count, patientId);

            return (redacted, ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PII redaction failed: correlation={CorrelationId} errorType={ErrorType} " +
                "patient={PatientId} (Edge Case 1).",
                correlationId, ex.GetType().Name, patientId);

            // Attempt failure audit log — best-effort, do not throw on failure here.
            try
            {
                await _auditService.LogEventAsync(
                    eventType:        "pii_redaction_failed",
                    actorUserId:      clinicianId,
                    targetEntityId:   patientId,
                    targetEntityType: "Patient",
                    metadata:         new Dictionary<string, string>
                    {
                        ["correlation_id"] = correlationId.ToString(),
                        ["error_type"]     = ex.GetType().Name,
                    },
                    ct: ct);
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx,
                    "Failed to write pii_redaction_failed audit log for correlation {CorrelationId}.",
                    correlationId);
            }

            throw new PiiRedactionFailureException(
                "PII redaction pipeline failed; AI request blocked to prevent PII exposure.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> DeAnonymizeAsync(
        string responseText,
        Guid correlationId,
        CancellationToken ct = default)
    {
        var tokenMap = await _mapStore.GetAsync(correlationId, ct);

        if (tokenMap is null || tokenMap.Count == 0)
        {
            _logger.LogDebug(
                "No redaction map for correlation {CorrelationId} — returning response unchanged.",
                correlationId);
            return responseText;
        }

        // Replace tokens in the LLM response with original values (AC-3).
        var restored = responseText;
        foreach (var (token, original) in tokenMap)
        {
            restored = restored.Replace(token, original, StringComparison.Ordinal);
        }

        // Explicit Redis cleanup — TTL is the safety net.
        await _mapStore.DeleteAsync(correlationId, ct);

        // Audit log — token count only, no raw values (AC-2, AC-3, NFR-010).
        await _auditService.LogEventAsync(
            eventType:        "pii_deanonymized",
            actorUserId:      Guid.Empty,  // system action post-response, no interactive actor
            targetEntityId:   null,
            targetEntityType: "RedactionContext",
            metadata:         new Dictionary<string, string>
            {
                ["correlation_id"]   = correlationId.ToString(),
                ["tokens_restored"]  = tokenMap.Count.ToString(),
            },
            ct: ct);

        _logger.LogInformation(
            "De-anonymization complete: correlation={CorrelationId} tokensRestored={Count} (AC-3).",
            correlationId, tokenMap.Count);

        return restored;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a regex that matches <c>field_name: value</c> or <c>"field_name": "value"</c>
    /// and captures the value in a named group <c>value</c>.
    /// </summary>
    private static Regex BuildStructuredFieldPattern(string field)
    {
        // Matches:
        //   patient_name: John Doe
        //   patient_name: "John Doe"
        //   "patient_name": "John Doe"
        //   patient_name = John Doe
        // Using verbatim interpolated string: "" → literal quote, {{ → literal {, }} → literal }
        var fieldEscaped = Regex.Escape(field);
        var pattern      = $@"(?i)""?{fieldEscaped}""?\s*[:=]\s*""?(?<value>[^""\n,{{}}]+)""?";
        return new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Generates a deterministic, opaque redaction token: <c>[REDACTED_{fieldType}_{hmacHex8}]</c>.
    /// HMAC-SHA256 of <paramref name="value"/> keyed with the configured HMAC key truncated to
    /// 8 hex characters — reversible only via the stored token map.
    /// </summary>
    private string GenerateToken(string fieldType, string value)
    {
        var key = ResolveHmacKey();
        using var hmac = new HMACSHA256(key);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        var hexShort  = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        return $"[REDACTED_{fieldType}_{hexShort}]";
    }

    /// <summary>Derives the HMAC key from configuration, with a dev-only fallback.</summary>
    private byte[] ResolveHmacKey()
    {
        if (!string.IsNullOrEmpty(_options.HmacKey))
            return Convert.FromBase64String(_options.HmacKey);

        _logger.LogWarning(
            "AI:Redaction:HmacKey is not configured. " +
            "Using insecure dev-only HMAC key — configure from secrets vault in production.");

        return SHA256.HashData("propeliq-dev-only-hmac-key-CHANGE-IN-PRODUCTION"u8.ToArray());
    }

    /// <summary>Normalises a structured field name to an upper-case type label (max 10 chars).</summary>
    private static string NormalizeFieldType(string field)
    {
        var upper = field.Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return upper.Length > 10 ? upper[..10] : upper;
    }

    /// <summary>Extracts the field-type segment from a token string <c>[REDACTED_FIELD_hash]</c>.</summary>
    private static string ExtractFieldTypeFromToken(string token)
    {
        // Token format: [REDACTED_FIELDTYPE_hash]
        var trimmed = token.Trim('[', ']');
        var parts   = trimmed.Split('_');
        return parts.Length >= 2 ? parts[1] : "UNKNOWN";
    }
}
