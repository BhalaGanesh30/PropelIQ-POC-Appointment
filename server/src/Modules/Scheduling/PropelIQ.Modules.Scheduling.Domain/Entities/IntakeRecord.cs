using System.Text.Json;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// Immutable finalized intake record created on form submission (AC-4).
/// Linked to the Appointment booking via AppointmentId.
/// </summary>
public sealed class IntakeRecord : BaseEntity
{
    /// <summary>Patient who submitted the intake form.</summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Appointment booking this intake is attached to (AC-4).
    /// Has a unique index — one intake record per appointment.
    /// </summary>
    public Guid AppointmentId { get; set; }

    /// <summary>JSONB column: the complete validated form data at time of submission.</summary>
    public JsonDocument FormData { get; set; } = default!;

    /// <summary>Field names that were populated by AI at time of submission.</summary>
    public List<string> AiPopulatedFields { get; set; } = [];

    /// <summary>UTC timestamp when the patient submitted the intake form.</summary>
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}
