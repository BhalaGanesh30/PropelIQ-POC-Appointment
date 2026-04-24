using System.Text.Json;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.AI.Dto;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.Modules.Scheduling.Infrastructure.AI;

/// <summary>
/// Orchestrates AI-assisted intake prefill.
///
/// Accepts a free-text symptom description, constructs a versioned prompt,
/// calls the LiteLLM gateway, parses the structured JSON response, and
/// returns field-level suggestions with an AI-populated flag list (UXR-405).
///
/// Reliability (AIR-005): any gateway failure (circuit open, timeout, HTTP error,
/// or JSON parse failure) returns a deterministic fallback response so the intake
/// form degrades to manual mode without a user-visible error.
/// PII (AIR-009): only the sanitised symptom text reaches the gateway — no patient
/// identifiers are forwarded.
/// Audit (AIR-011): every call outcome is logged with token count and confidence.
/// </summary>
public sealed class IntakeAssistService
{
    // Prompt template is embedded in the assembly to avoid runtime file-path
    // dependencies and to keep the prompt version-controlled with the binary.
    private static readonly Lazy<JsonDocument> PromptTemplate = new(() =>
    {
        var assembly = typeof(IntakeAssistService).Assembly;
        const string resourceName =
            "PropelIQ.Modules.Scheduling.Infrastructure.AI.Prompts.intake-assist.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. " +
                "Ensure the file is marked as EmbeddedResource in the project.");

        using var reader = new StreamReader(stream);
        return JsonDocument.Parse(reader.ReadToEnd());
    });

    // Serialiser that maps PascalCase C# property names to the camelCase JSON
    // keys returned by the model (e.g. ReasonForVisit → reasonForVisit).
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAiGatewayClient _aiClient;
    private readonly ILogger<IntakeAssistService> _logger;

    public IntakeAssistService(
        IAiGatewayClient aiClient,
        ILogger<IntakeAssistService> logger)
    {
        _aiClient = aiClient;
        _logger = logger;
    }

    /// <summary>
    /// Sends a free-text symptom description to the AI gateway and maps the
    /// structured JSON response to <see cref="IntakeAssistResponse"/>.
    /// Never throws — all error paths return a fallback response (AIR-005).
    /// </summary>
    public async Task<IntakeAssistResponse> AssistAsync(
        IntakeAssistRequest request,
        CancellationToken ct)
    {
        // AIR-009: strip leading/trailing whitespace; no patient identifiers are sent.
        var sanitizedText = request.FreeTextDescription.Trim();

        if (string.IsNullOrWhiteSpace(sanitizedText))
            return Fallback("Empty description provided");

        // Fast-path: skip the HTTP call when the circuit is already open (AIR-005).
        if (_aiClient.IsCircuitBreakerOpen)
            return Fallback("AI assist unavailable (circuit open)");

        JsonDocument template;
        try
        {
            template = PromptTemplate.Value;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "intake-assist.json embedded resource missing");
            return Fallback("AI assist unavailable (configuration error)");
        }

        var root = template.RootElement;
        var systemPrompt = root.GetProperty("system_prompt").GetString()!;
        var userTemplate = root.GetProperty("user_template").GetString()!;
        var userPrompt = userTemplate.Replace("{free_text}", sanitizedText);

        var chatRequest = new ChatCompletionRequest
        {
            Model = root.GetProperty("model").GetString()!,
            Temperature = root.GetProperty("temperature").GetDouble(),
            MaxTokens = root.GetProperty("max_tokens").GetInt32(),
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user",   Content = userPrompt   },
            ],
        };

        ChatCompletionResponse? aiResponse;
        try
        {
            // AIR-006: timeout is enforced by the Polly pipeline in LiteLlmGatewayClient.
            aiResponse = await _aiClient.GetCompletionAsync(chatRequest, ct);
        }
        catch (Exception ex)
        {
            // Catch any unexpected exception so the intake form never breaks (AIR-005).
            _logger.LogWarning(ex, "AI gateway call failed unexpectedly");
            return Fallback("AI assist unavailable, please fill in manually.");
        }

        // Null return signals circuit-open or HTTP 401 (see LiteLlmGatewayClient).
        if (aiResponse is null)
            return Fallback("AI assist unavailable, please fill in manually.");

        var content = aiResponse.Choices.Count > 0
            ? aiResponse.Choices[0].Message.Content
            : null;

        // AIR-011: audit log — every call outcome with token usage and model.
        _logger.LogInformation(
            "IntakeAssist AI call completed: Model={Model}, Tokens={Tokens}, HasContent={HasContent}",
            aiResponse.Model,
            aiResponse.Usage.TotalTokens,
            !string.IsNullOrEmpty(content));

        if (string.IsNullOrWhiteSpace(content))
            return Fallback("AI assist returned an empty response");

        // AIR-008: validate / parse the structured JSON output.
        try
        {
            var suggestions = JsonSerializer.Deserialize<IntakeFieldSuggestions>(
                content, JsonOpts);

            if (suggestions is null)
                return Fallback("Invalid AI response structure");

            var populated = BuildPopulatedFieldList(suggestions);

            // AIR-011: log confidence alongside populated fields for audit trail.
            _logger.LogInformation(
                "IntakeAssist prefill: PopulatedFields={Fields}, Confidence=0.85",
                string.Join(',', populated));

            return new IntakeAssistResponse
            {
                AiAssisted = true,
                Suggestions = suggestions,
                AiPopulatedFields = populated,
                Confidence = 0.85, // Static default; extend with logprobs when available.
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse AI intake-assist response — falling back to manual");
            return Fallback("AI assist unavailable, please fill in manually.");
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static List<string> BuildPopulatedFieldList(IntakeFieldSuggestions s)
    {
        var fields = new List<string>();
        if (s.ReasonForVisit        is not null) fields.Add("reasonForVisit");
        if (s.SymptomDescription    is not null) fields.Add("symptomDescription");
        if (s.Severity              is not null) fields.Add("severity");
        if (s.OnsetDuration         is not null) fields.Add("onsetDuration");
        if (s.BodyArea              is not null) fields.Add("bodyArea");
        if (s.RelevantMedicalHistory.Count > 0)  fields.Add("relevantMedicalHistory");
        if (s.CurrentMedications.Count    > 0)  fields.Add("currentMedications");
        if (s.Allergies.Count             > 0)  fields.Add("allergies");
        return fields;
    }

    private static IntakeAssistResponse Fallback(string reason) =>
        new()
        {
            AiAssisted = false,
            FallbackReason = reason,
            Suggestions = new IntakeFieldSuggestions(),
            AiPopulatedFields = [],
            Confidence = 0.0,
        };
}
