using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace PropelIQ.Modules.Scheduling.Application.Intake.Dto;

/// <summary>
/// Autosave request — submitted on each blur event (AC-2).
/// Contains partial form data and the optional target slot.
/// </summary>
public record SaveDraftRequest
{
    public Guid? SlotId { get; init; }

    /// <summary>Partial form field values as a JSON object.</summary>
    [Required]
    public JsonDocument FormData { get; init; } = default!;

    /// <summary>Field names pre-populated by the AI assistant (may be null when no AI assist).</summary>
    public List<string>? AiPopulatedFields { get; init; }
}

/// <summary>
/// Response after a successful autosave — supplies the timestamp for the "Saved" indicator (AC-2).
/// </summary>
public record SaveDraftResponse
{
    public Guid DraftId { get; init; }
    public DateTimeOffset SavedAt { get; init; }
}

/// <summary>
/// Draft data returned for resume-from-where-left-off (AC-3).
/// </summary>
public record IntakeDraftResponse
{
    public Guid Id { get; init; }
    public Guid? SlotId { get; init; }

    /// <summary>All saved form field values.</summary>
    public JsonDocument FormData { get; init; } = default!;

    /// <summary>Field names that were AI-populated when the draft was saved.</summary>
    public List<string> AiPopulatedFields { get; init; } = [];

    public string Status { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Request to finalize and submit the intake form (AC-4).
/// </summary>
public record SubmitIntakeRequest
{
    public Guid DraftId { get; init; }

    /// <summary>Appointment booking to attach this intake record to (AC-4).</summary>
    public Guid AppointmentId { get; init; }
}

/// <summary>
/// Response after successful intake submission (AC-4).
/// </summary>
public record SubmitIntakeResponse
{
    public Guid IntakeRecordId { get; init; }

    /// <summary>The appointment this intake record was attached to.</summary>
    public Guid AppointmentId { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }
}
