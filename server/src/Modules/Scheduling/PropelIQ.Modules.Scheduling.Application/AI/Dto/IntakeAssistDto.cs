namespace PropelIQ.Modules.Scheduling.Application.AI.Dto;

/// <summary>
/// Free-text patient symptom description submitted for AI-assisted intake prefill.
/// Only symptom text is accepted — no patient identifiers are forwarded to the AI gateway (AIR-009).
/// </summary>
public record IntakeAssistRequest
{
    /// <summary>Free-text description of the patient's reason for visit / symptoms.</summary>
    public string FreeTextDescription { get; init; } = string.Empty;

    /// <summary>Preferred language for suggestions (BCP-47). Defaults to English.</summary>
    public string? Language { get; init; } = "en";
}

/// <summary>
/// AI-assisted intake prefill response.
/// When <see cref="AiAssisted"/> is false the caller falls back to manual mode (AIR-005).
/// </summary>
public record IntakeAssistResponse
{
    /// <summary>True when the AI gateway returned a parseable, structured response.</summary>
    public bool AiAssisted { get; init; }

    /// <summary>Human-readable reason for fallback when <see cref="AiAssisted"/> is false.</summary>
    public string? FallbackReason { get; init; }

    /// <summary>Structured intake field suggestions extracted by the model.</summary>
    public IntakeFieldSuggestions Suggestions { get; init; } = new();

    /// <summary>
    /// Names of fields populated by AI so the frontend can render the AI-populated badge (UXR-405).
    /// </summary>
    public List<string> AiPopulatedFields { get; init; } = [];

    /// <summary>Model confidence score in [0, 1]. Zero indicates a fallback response.</summary>
    public double Confidence { get; init; }
}

/// <summary>
/// Structured intake fields extracted from the patient's free-text description.
/// </summary>
public record IntakeFieldSuggestions
{
    /// <summary>Chief complaint / reason for visit.</summary>
    public string? ReasonForVisit { get; init; }

    /// <summary>Detailed symptom description.</summary>
    public string? SymptomDescription { get; init; }

    /// <summary>Symptom severity: Mild, Moderate, or Severe.</summary>
    public string? Severity { get; init; }

    /// <summary>When symptoms started (e.g. "3 days ago").</summary>
    public string? OnsetDuration { get; init; }

    /// <summary>Affected body area.</summary>
    public string? BodyArea { get; init; }

    /// <summary>Relevant medical history conditions mentioned by the patient.</summary>
    public List<string> RelevantMedicalHistory { get; init; } = [];

    /// <summary>Current medications mentioned by the patient.</summary>
    public List<string> CurrentMedications { get; init; } = [];

    /// <summary>Allergies mentioned by the patient.</summary>
    public List<string> Allergies { get; init; } = [];
}
