using System.Text.Json;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.Api.Infrastructure;

/// <summary>
/// Development-only AI gateway stub.
/// Returns plausible structured intake field suggestions by performing simple
/// keyword analysis on the free-text input — no external service required.
///
/// NEVER registered in Production. Registered conditionally in Program.cs:
///   if (isDevelopment) services.AddSingleton&lt;IAiGatewayClient, DevMockAiGatewayClient&gt;()
///
/// This overrides the LiteLlmGatewayClient registered by AddAiGateway() so the AI-assist
/// flow works end-to-end during local development without LiteLLM or Azure OpenAI.
/// </summary>
public sealed class DevMockAiGatewayClient : IAiGatewayClient
{
    // camelCase serialisation matches what IntakeAssistService.JsonOpts expects.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public bool IsCircuitBreakerOpen => false;

    /// <inheritdoc />
    public Task<ChatCompletionResponse?> GetCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userMessage = request.Messages
            .LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

        var suggestions = BuildSuggestions(userMessage);
        var content = JsonSerializer.Serialize(suggestions, JsonOpts);

        var response = new ChatCompletionResponse
        {
            Id = $"dev-mock-{Guid.NewGuid():N}",
            Model = "dev-mock",
            Choices =
            [
                new Choice
                {
                    Message = new ChatMessage { Role = "assistant", Content = content },
                    FinishReason = "stop",
                },
            ],
            Usage = new UsageInfo
            {
                PromptTokens = 80,
                CompletionTokens = 70,
                TotalTokens = 150,
            },
        };

        return Task.FromResult<ChatCompletionResponse?>(response);
    }

    // ── Keyword-based suggestion builder ─────────────────────────────────────

    /// <summary>
    /// Extracts the raw free-text entered by the patient from the rendered prompt template.
    /// IntakeAssistService builds the user message as:
    ///   Patient describes their reason for visit:\n\n"&lt;free_text&gt;"\n\nExtract...
    /// We retrieve only the text inside the first quoted block.
    /// </summary>
    private static string ExtractFreeText(string userMessage)
    {
        var start = userMessage.IndexOf('"');
        if (start < 0) return userMessage.Trim();
        var end = userMessage.IndexOf('"', start + 1);
        if (end < 0) return userMessage.Trim();
        return userMessage[(start + 1)..end].Trim();
    }

    private static object BuildSuggestions(string userMessage)
    {
        var freeText = ExtractFreeText(userMessage);
        var lower = freeText.ToLowerInvariant();

        return new
        {
            reasonForVisit     = ExtractChiefComplaint(lower),
            symptomDescription = freeText.Length > 0 ? freeText : (string?)null,
            severity           = ExtractSeverity(lower),
            onsetDuration      = ExtractOnset(lower),
            bodyArea           = ExtractBodyArea(lower),
            relevantMedicalHistory = Array.Empty<string>(),
            currentMedications     = Array.Empty<string>(),
            allergies              = Array.Empty<string>(),
        };
    }

    private static string? ExtractChiefComplaint(string lower)
    {
        if (lower.Contains("headache") || lower.Contains("head ache")) return "Headache";
        if (lower.Contains("migraine"))   return "Migraine";
        if (lower.Contains("chest pain")) return "Chest pain";
        if (lower.Contains("back pain"))  return "Back pain";
        if (lower.Contains("sore throat") || lower.Contains("throat")) return "Sore throat";
        if (lower.Contains("abdominal") || lower.Contains("stomach pain")) return "Abdominal pain";
        if (lower.Contains("nausea") || lower.Contains("vomit"))  return "Nausea and vomiting";
        if (lower.Contains("dizziness") || lower.Contains("dizzy")) return "Dizziness";
        if (lower.Contains("fatigue") || lower.Contains("tired") || lower.Contains("exhausted")) return "Fatigue";
        if (lower.Contains("fever") || lower.Contains("temperature")) return "Fever";
        if (lower.Contains("cough")) return "Cough";
        if (lower.Contains("rash") || lower.Contains("itch")) return "Skin rash / itching";
        if (lower.Contains("pain")) return "Pain";
        if (lower.Contains("swelling") || lower.Contains("swollen")) return "Swelling";
        return "General complaint";
    }

    private static string ExtractSeverity(string lower)
    {
        if (lower.Contains("severe") || lower.Contains("unbearable") || lower.Contains("worst") ||
            lower.Contains("terrible") || lower.Contains("extreme")) return "Severe";
        if (lower.Contains("mild") || lower.Contains("slight") || lower.Contains("little") ||
            lower.Contains("minor")) return "Mild";
        return "Moderate";
    }

    private static string? ExtractOnset(string lower)
    {
        // Specific day counts
        if (lower.Contains("1 day") || lower.Contains("one day") || lower.Contains("since yesterday")) return "1 day";
        if (lower.Contains("2 day") || lower.Contains("two day")) return "2 days";
        if (lower.Contains("3 day") || lower.Contains("three day")) return "3 days";
        if (lower.Contains("4 day") || lower.Contains("four day")) return "4 days";
        if (lower.Contains("5 day") || lower.Contains("five day")) return "5 days";
        if (lower.Contains("1 week") || lower.Contains("one week") || lower.Contains("a week")) return "1 week";
        if (lower.Contains("2 week") || lower.Contains("two week")) return "2 weeks";
        if (lower.Contains("1 month") || lower.Contains("one month") || lower.Contains("a month")) return "1 month";
        if (lower.Contains("few day")) return "A few days";
        if (lower.Contains("few week")) return "A few weeks";
        if (lower.Contains("this morning") || lower.Contains("today")) return "Today";
        if (lower.Contains("yesterday")) return "Since yesterday";
        if (lower.Contains("past week") || lower.Contains("last week")) return "About 1 week";
        if (lower.Contains("past month") || lower.Contains("last month")) return "About 1 month";
        return null;
    }

    private static string? ExtractBodyArea(string lower)
    {
        if (lower.Contains("head") || lower.Contains("forehead") || lower.Contains("skull")) return "Head";
        if (lower.Contains("migraine") || lower.Contains("temple")) return "Head (temples)";
        if (lower.Contains("eye") || lower.Contains("vision")) return "Eyes";
        if (lower.Contains("ear")) return "Ears";
        if (lower.Contains("throat") || lower.Contains("neck")) return "Throat / Neck";
        if (lower.Contains("chest") || lower.Contains("heart") || lower.Contains("lung")) return "Chest";
        if (lower.Contains("stomach") || lower.Contains("abdomen") || lower.Contains("belly")) return "Abdomen";
        if (lower.Contains("lower back") || lower.Contains("lumbar")) return "Lower back";
        if (lower.Contains("back")) return "Back";
        if (lower.Contains("shoulder")) return "Shoulder";
        if (lower.Contains("arm") || lower.Contains("elbow") || lower.Contains("wrist")) return "Arm";
        if (lower.Contains("hand") || lower.Contains("finger")) return "Hand";
        if (lower.Contains("hip")) return "Hip";
        if (lower.Contains("knee")) return "Knee";
        if (lower.Contains("leg") || lower.Contains("calf") || lower.Contains("ankle")) return "Leg";
        if (lower.Contains("foot") || lower.Contains("feet") || lower.Contains("toe")) return "Foot";
        if (lower.Contains("skin") || lower.Contains("rash")) return "Skin (general)";
        return null;
    }
}
