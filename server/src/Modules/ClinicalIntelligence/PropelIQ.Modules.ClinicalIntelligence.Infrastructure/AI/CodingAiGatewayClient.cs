using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.SharedServices.Application.AiAudit;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.AiGateway.Models;
using SharedAI = PropelIQ.Modules.SharedServices.Application.AI;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// GPT-4.1 coding suggestion client via LiteLLM proxy (US_049, US_054, AIR-006).
///
/// Delegates HTTP transport + Polly resilience (circuit-breaker + retry) to the shared
/// <see cref="IAiGatewayClient"/> so this class focuses only on prompt engineering,
/// PII redaction, and response extraction.
///
/// PII pipeline (US_054):
/// <list type="number">
///   <item>Context chunks are ACL-validated (AC-4, AIR-010).</item>
///   <item>The assembled user message is PII-redacted before being sent to the model (AC-1).</item>
///   <item>The model response is de-anonymized before being returned to the orchestrator (AC-3).</item>
/// </list>
///
/// On <see cref="PiiRedactionFailureException"/> or <see cref="ACLViolationException"/>:
/// returns <c>null</c> (manual fallback path — Edge Case 1, AIR-005).
///
/// Structured output schema: the system prompt instructs GPT-4.1 to return a JSON
/// object with a <c>suggestions</c> array where each item has the required fields.
/// The model temperature is kept low (0.1) for deterministic code recommendations.
/// </summary>
internal sealed class CodingAiGatewayClient : ICodingAiGatewayClient
{
    private const string CodingModel = "coding-suggestion";
    private const double Temperature = 0.1;
    private const int    MaxTokens   = 2048;

    private static readonly string SystemPrompt = """
        You are a clinical coding specialist. Given a set of clinical facts extracted from
        a patient's medical records, suggest the most appropriate ICD-10-CM diagnosis codes.

        Return ONLY a valid JSON object with no additional text, markdown, or explanation.
        The JSON schema is:
        {
          "suggestions": [
            {
              "icd10_code": "string (e.g. J18.9)",
              "description": "string (human-readable ICD-10 description)",
              "confidence": <number 0.0-1.0>,
              "rationale": "string (clinical reasoning referencing the facts)",
              "fact_ids": ["guid", ...]
            }
          ]
        }

        Rules:
        - Return up to 3 suggestions sorted by confidence descending.
        - Each confidence value must be between 0.0 and 1.0.
        - fact_ids must reference only the fact IDs provided in the context.
        - Do not reproduce or infer any patient PII.
        - Do not suggest codes unsupported by the evidence.
        """;

    private readonly IAiGatewayClient                   _gateway;
    private readonly SharedAI.IPiiRedactionService      _piiRedaction;
    private readonly SharedAI.IPatientContextAclFilter  _aclFilter;
    private readonly IAiAuditService                    _aiAudit;
    private readonly ILogger<CodingAiGatewayClient>     _logger;

    public CodingAiGatewayClient(
        IAiGatewayClient gateway,
        SharedAI.IPiiRedactionService piiRedaction,
        SharedAI.IPatientContextAclFilter aclFilter,
        IAiAuditService aiAudit,
        ILogger<CodingAiGatewayClient> logger)
    {
        _gateway      = gateway;
        _piiRedaction = piiRedaction;
        _aclFilter    = aclFilter;
        _aiAudit      = aiAudit;
        _logger       = logger;
    }

    /// <inheritdoc />
    public async Task<string?> RequestSuggestionsAsync(
        IReadOnlyList<EvidenceChunk> evidence,
        Guid patientId,
        Guid clinicianId,
        CancellationToken ct = default)
    {
        if (_gateway.IsCircuitBreakerOpen)
        {
            _logger.LogWarning(
                "AI gateway circuit breaker is open. Skipping coding suggestion request (AIR-005).");
            return null;
        }

        // ── Step 1: ACL filter — defence-in-depth, verifies all chunks belong to patientId ──
        // Converts EvidenceChunks to ContextChunks, tagging each with the request patientId.
        // The primary ACL is at the pgvector query level; this is a secondary runtime guard (AC-4).
        IReadOnlyList<SharedAI.ContextChunk> contextChunks;
        try
        {
            contextChunks = evidence
                .Select(e => new SharedAI.ContextChunk(
                    FactId:    e.FactId,
                    PatientId: patientId,
                    FactType:  e.FactType,
                    Content:   $"{e.Name}: {e.Value}"))
                .ToList();

            await _aclFilter.ValidateAsync(contextChunks, patientId, clinicianId, ct);
        }
        catch (SharedAI.ACLViolationException ex)
        {
            _logger.LogError(ex,
                "ACL violation during context assembly for patient {PatientId} (clinician {ClinicianId}). " +
                "AI request blocked (AC-4, AIR-010).",
                patientId, clinicianId);
            return null;
        }

        // ── Step 2: Build user message and apply PII redaction (US_054, AC-1) ──
        var rawUserMessage = BuildUserMessage(evidence);

        string redactedUserMessage;
        Guid   correlationId;

        try
        {
            var (redacted, ctx) = await _piiRedaction.RedactAsync(rawUserMessage, patientId, clinicianId, ct);
            redactedUserMessage = redacted;
            correlationId       = ctx.CorrelationId;
        }
        catch (SharedAI.PiiRedactionFailureException ex)
        {
            _logger.LogError(ex,
                "PII redaction failed for patient {PatientId} (clinician {ClinicianId}). " +
                "AI request blocked to prevent PII exposure (Edge Case 1).",
                patientId, clinicianId);
            return null;
        }

        // ── Step 3: Dispatch to AI gateway ──
        var requestTimestamp = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        var request = new ChatCompletionRequest
        {
            Model       = CodingModel,
            Temperature = Temperature,
            MaxTokens   = MaxTokens,
            Messages    =
            [
                new ChatMessage { Role = "system", Content = SystemPrompt        },
                new ChatMessage { Role = "user",   Content = redactedUserMessage },
            ],
        };

        var response = await _gateway.GetCompletionAsync(request, ct);
        sw.Stop();

        if (response is null)
        {
            _logger.LogWarning(
                "AI gateway returned null for coding suggestion (patient {PatientId}).",
                patientId);
            return null;
        }

        var content = response.Choices.FirstOrDefault()?.Message.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning(
                "AI gateway returned empty content for coding suggestion (patient {PatientId}).",
                patientId);
            return null;
        }

        // ── Step 4: De-anonymize LLM response (US_054, AC-3) ──
        var deAnonymized = await _piiRedaction.DeAnonymizeAsync(content, correlationId, ct);

        // ── Step 5: Fire-and-forget AI audit log (US_055, AIR-011) ──
        // Uses _ discard + CancellationToken.None: audit must not be cancelled by client disconnect.
        var contextRefsJson = JsonSerializer.Serialize(
            contextChunks.Select(c => new { factId = c.FactId, factType = c.FactType }));
        var promptHash      = ComputePromptHash(redactedUserMessage);

        _ = _aiAudit.LogAiRequestAsync(new AiAuditEntry(
            AiRequestId:      correlationId,
            RequestTimestamp: requestTimestamp,
            ClinicianId:      clinicianId,
            PromptHash:       promptHash,
            ContextRefs:      contextRefsJson,
            ModelName:        CodingModel,
            ResponsePayload:  deAnonymized,
            ConfidenceScores: "{}",
            LatencyMs:        (int)sw.ElapsedMilliseconds,
            FallbackReason:   null),
            CancellationToken.None);

        return deAnonymized;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static string ComputePromptHash(string redactedPrompt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(redactedPrompt));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildUserMessage(IReadOnlyList<EvidenceChunk> evidence)
    {
        var sb = new StringBuilder("Clinical evidence for ICD-10 code suggestion:\n\n");

        foreach (var chunk in evidence)
        {
            sb.AppendLine($"- FactId: {chunk.FactId}");
            sb.AppendLine($"  Type: {chunk.FactType}");
            sb.AppendLine($"  Name: {chunk.Name}");
            sb.AppendLine($"  Value: {chunk.Value}");
            if (chunk.FactDate.HasValue)
            {
                sb.AppendLine($"  Date: {chunk.FactDate.Value:yyyy-MM-dd}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Based on the above clinical facts, suggest ICD-10-CM codes. " +
                      "Return only the JSON object as specified.");

        return sb.ToString();
    }
}

