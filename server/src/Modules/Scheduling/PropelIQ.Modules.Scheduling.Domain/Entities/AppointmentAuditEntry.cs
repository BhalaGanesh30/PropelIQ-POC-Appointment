namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// Immutable audit record created whenever a staff member uses an override to
/// cancel or reschedule an appointment within the 24-hour policy window (AC-4).
/// NFR-010: all override actions must be auditable with actor, reason, and state snapshot.
/// DR-005: audit entries are append-only — no updates or deletes.
/// </summary>
public sealed class AppointmentAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }

    /// <summary>Staff user ID who performed the override (AC-4).</summary>
    public Guid PerformedByUserId { get; set; }

    /// <summary>"Cancel" or "Reschedule" — what action was taken.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Mandatory override reason captured at time of action (AC-4).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the action bypassed the 24-hour gate.</summary>
    public bool IsOverride { get; set; }

    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── State snapshot for audit trail ───────────────────────────────────────
    public string? PreviousStatus { get; set; }
    public Guid? PreviousSlotId { get; set; }
    public Guid? NewSlotId { get; set; }
}
