using System.Text.Json;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// Stores a patient's partially-completed intake form as a JSONB document.
/// Associated with a patient and optionally a slot — enables resume-from-where-left-off (AC-3).
/// Expires 7 days after creation to satisfy the session-expiry retention policy.
/// </summary>
public sealed class IntakeDraft : BaseEntity
{
    /// <summary>Patient owner — all draft operations are scoped to this ID.</summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Optional appointment slot this draft belongs to.
    /// Null when the draft was started without a selected slot.
    /// </summary>
    public Guid? SlotId { get; set; }

    /// <summary>Current lifecycle state (Draft → Submitted or Expired).</summary>
    public IntakeStatus Status { get; set; } = IntakeStatus.Draft;

    /// <summary>JSONB column: partial form field values keyed by field name.</summary>
    public JsonDocument FormData { get; set; } = JsonDocument.Parse("{}");

    /// <summary>List of field names that were pre-populated by the AI assistant.</summary>
    public List<string> AiPopulatedFields { get; set; } = [];

    /// <summary>
    /// Absolute expiry — drafts older than 7 days are marked Expired by the cleanup service.
    /// Edge case: session expiry does not delete the draft.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; } =
        DateTimeOffset.UtcNow.AddDays(7);

    /// <summary>Mutate form data and refresh the UpdatedAt timestamp (AC-2 autosave).</summary>
    public void Update(JsonDocument formData, List<string> aiPopulatedFields)
    {
        FormData = formData;
        AiPopulatedFields = aiPopulatedFields;
        MarkUpdated();
    }

    /// <summary>Transition to Submitted — called on final form submission (AC-4).</summary>
    public void Submit()
    {
        Status = IntakeStatus.Submitted;
        MarkUpdated();
    }
}
