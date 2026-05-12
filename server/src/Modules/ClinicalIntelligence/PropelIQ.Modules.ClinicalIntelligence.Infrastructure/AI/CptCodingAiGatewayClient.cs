using System.Text;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// GPT-4.1 CPT/E/M suggestion client via LiteLLM proxy (US_050, AIR-006).
///
/// Assembles a CPT-specific prompt including:
/// - Appointment type context (for E/M level determination).
/// - Clinical evidence chunks (ACL-filtered via <see cref="EvidenceRetrievalService"/>).
/// - JSON output schema instructions for CPT codes and E/M level.
///
/// Temperature is set to 0.1 for deterministic, reproducible code recommendations.
/// </summary>
internal sealed class CptCodingAiGatewayClient : ICptCodingAiGatewayClient
{
    private const string CptModel = "coding-suggestion";
    private const double Temperature = 0.1;
    private const int MaxTokens = 2048;

    private static readonly string SystemPrompt = """
        You are a clinical coding specialist with expertise in CPT (Current Procedural Terminology)
        procedure codes and E/M (Evaluation and Management) level assignment.

        Given a set of clinical facts extracted from a patient's medical records and the type of
        appointment, suggest the most appropriate CPT procedure codes and assign the appropriate
        E/M level.

        Return ONLY a valid JSON object with no additional text, markdown, or explanation.
        The JSON schema is:
        {
          "cpt_suggestions": [
            {
              "cpt_code": "string (5-digit CPT code, e.g. '99213')",
              "description": "string (human-readable CPT procedure description)",
              "confidence": <number 0.0-1.0>,
              "rationale": "string (clinical reasoning referencing the facts)",
              "fact_ids": ["guid", ...]
            }
          ],
          "em_suggestion": {
            "em_level": "string (E/M code, e.g. '99213')",
            "description": "string (human-readable E/M level description)",
            "confidence": <number 0.0-1.0>,
            "rationale": "string (clinical reasoning for E/M level)",
            "complexity_factors": ["string", ...]
          }
        }

        Rules:
        - Return up to 3 CPT suggestions sorted by confidence descending.
        - Each confidence value must be between 0.0 and 1.0.
        - fact_ids must reference only the fact IDs provided in the context.
        - complexity_factors must describe the clinical factors contributing to the E/M level.
        - Do not reproduce or infer any patient PII.
        - Do not suggest codes not supported by the evidence.
        - Do not suggest deprecated or invalid CPT codes.
        """;

    private readonly IAiGatewayClient _gateway;
    private readonly ILogger<CptCodingAiGatewayClient> _logger;

    public CptCodingAiGatewayClient(
        IAiGatewayClient gateway,
        ILogger<CptCodingAiGatewayClient> logger)
    {
        _gateway = gateway;
        _logger  = logger;
    }

    /// <inheritdoc />
    public async Task<string?> RequestCptSuggestionsAsync(
        string appointmentType,
        IReadOnlyList<EvidenceChunk> evidence,
        CancellationToken ct = default)
    {
        if (_gateway.IsCircuitBreakerOpen)
        {
            _logger.LogWarning(
                "AI gateway circuit breaker is open. Skipping CPT suggestion request (AIR-005).");
            return null;
        }

        var userMessage = BuildUserMessage(appointmentType, evidence);

        var request = new ChatCompletionRequest
        {
            Model       = CptModel,
            Temperature = Temperature,
            MaxTokens   = MaxTokens,
            Messages    =
            [
                new ChatMessage { Role = "system", Content = SystemPrompt },
                new ChatMessage { Role = "user",   Content = userMessage  },
            ],
        };

        var response = await _gateway.GetCompletionAsync(request, ct);
        if (response is null)
        {
            _logger.LogWarning("AI gateway returned null for CPT suggestion request.");
            return null;
        }

        var content = response.Choices.FirstOrDefault()?.Message.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("AI gateway returned empty content for CPT suggestion.");
            return null;
        }

        return content;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static string BuildUserMessage(string appointmentType, IReadOnlyList<EvidenceChunk> evidence)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Appointment Type: {appointmentType}");
        sb.AppendLine();
        sb.AppendLine("Clinical evidence for CPT/E/M code suggestion:");
        sb.AppendLine();

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

        return sb.ToString();
    }
}
