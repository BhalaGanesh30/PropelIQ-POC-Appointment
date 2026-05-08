namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// A single event on the clinical timeline (US_048 AC-1).
///
/// Produced from two sources:
///   - <c>clinical_facts</c>: eventType = "fact_added", categories = Medications / Allergies /
///     Diagnoses / Findings, eventDate = fact_date ?? created_at.
///   - <c>clinical_documents</c>: eventType = "document_uploaded", category = "Documents",
///     eventDate = created_at.
/// </summary>
public sealed record TimelineEventDto
{
    /// <summary>Primary key of the source row (fact GUID or document GUID).</summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Source event type discriminator: "fact_added" or "document_uploaded".
    /// Allows the frontend to link to the correct detail view.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Human-readable category: "Medications", "Allergies", "Diagnoses", "Findings",
    /// or "Documents" (US_048 AC-1, AC-2).
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Human-readable description composed from the source entity.
    /// Facts: "{Name}: {Value}" (or "{Value}" when Name is null).
    /// Documents: display_name (falls back to file_name when null).
    /// </summary>
    public required string Description { get; init; }

    /// <summary>Date/time of the clinical event — used for reverse-chronological sorting (AC-1).</summary>
    public required DateTimeOffset EventDate { get; init; }

    /// <summary>Patient the event belongs to — allows cache key scoping.</summary>
    public required Guid PatientId { get; init; }
}
