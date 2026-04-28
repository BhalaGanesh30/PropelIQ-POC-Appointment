namespace PropelIQ.Modules.Scheduling.Application.AI.Prompts;

/// <summary>
/// Builds the structured no-show risk scoring prompt sent to the AI gateway.
/// AIR-009: Only aggregated history counts and appointment metadata are included —
/// no patient names, contact details, or direct identifiers are present.
/// </summary>
public static class NoShowRiskPrompt
{
    /// <summary>
    /// Model alias configured in the LiteLLM proxy config.yaml.
    /// Routed to Azure OpenAI GPT-4.1 family via the gateway.
    /// </summary>
    public const string ModelAlias = "no-show-risk";

    public const string SystemPrompt =
        "You are a medical appointment no-show risk classifier. " +
        "Analyze the provided patient appointment features and classify " +
        "the no-show risk as Low, Medium, or High. " +
        "Respond ONLY with valid JSON matching the requested schema — " +
        "no markdown, no preamble, no explanation outside the JSON object.";

    /// <summary>
    /// Constructs a user-turn prompt from aggregated patient history features.
    /// AIR-009: No PII — only counts, rates, and appointment metadata are included.
    /// </summary>
    public static string Build(PatientHistoryFeatures features) =>
        $$"""
        Analyze the following patient appointment features and classify the no-show risk.

        Patient History (aggregated — no PII):
        - Total past appointments: {{features.TotalAppointments}}
        - Previous no-shows: {{features.NoShowCount}}
        - Previous cancellations: {{features.CancellationCount}}
        - Confirmed via reminder: {{features.ConfirmedViaReminderCount}}
        - Average booking lead time: {{features.AverageLeadTimeDays}} days
        - Appointment day of week: {{features.DayOfWeek}}
        - Appointment time of day: {{features.TimeOfDay}}

        Respond with ONLY this JSON schema:
        {
          "riskLevel": "Low" | "Medium" | "High",
          "confidence": <float 0.0–1.0>,
          "features": [
            { "name": "<feature name>", "contribution": "<impact explanation>" }
          ]
        }

        Include 3–5 feature contributions. Each must reference a specific input
        feature above and describe its impact direction (increases/decreases risk).
        """;
}
