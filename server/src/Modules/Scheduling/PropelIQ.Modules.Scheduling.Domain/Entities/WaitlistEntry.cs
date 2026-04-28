using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// Preferred-slot waitlist entry (US_023).
///
/// AC-1: Persists patient preferred slot parameters (date range, duration, type).
/// AC-2: Tracks the offered slot and the 2-hour claim expiry timestamp.
/// AC-3: Transitions to Claimed when the patient successfully reserves the slot.
/// AC-4: Transitions to Expired when the claim window lapses; slot rotates to next patient.
/// FIFO: <see cref="Position"/> (assigned at creation) is the primary ordering key.
/// </summary>
public sealed class WaitlistEntry : BaseEntity
{
    public required Guid PatientId { get; set; }

    /// <summary>Enum status stored as string in DB for readability.</summary>
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Active;

    // ── Preferred slot parameters (AC-1) ─────────────────────────────────────

    public DateTimeOffset PreferredDateStart { get; set; }
    public DateTimeOffset PreferredDateEnd { get; set; }

    /// <summary>Allowed values: 15, 30, 60 (mirrors <see cref="SlotDuration"/>).</summary>
    public int PreferredDurationMinutes { get; set; }

    /// <summary>Appointment type string (e.g. "General", "Specialist").</summary>
    public string PreferredAppointmentType { get; set; } = string.Empty;

    // ── Offer lifecycle (AC-2, AC-4) ──────────────────────────────────────────

    /// <summary>Slot offered when a matching cancellation or release occurs.</summary>
    public Guid? OfferedSlotId { get; set; }

    /// <summary>UTC timestamp when the slot was offered to this patient.</summary>
    public DateTimeOffset? OfferedAt { get; set; }

    /// <summary>UTC timestamp when the 2-hour claim window closes (AC-4).</summary>
    public DateTimeOffset? ClaimExpiresAt { get; set; }

    /// <summary>
    /// SHA-256 hash (hex) of the HMAC-signed claim token embedded in the alert
    /// email / SMS link.  Verified on claim to prevent unauthorised slot reservation.
    /// Null until the slot alert has been dispatched (US_030 task_001).
    /// </summary>
    public string? ClaimTokenHash { get; set; }

    // ── Outcome timestamps ─────────────────────────────────────────────────────

    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    // ── FIFO ordering ─────────────────────────────────────────────────────────

    /// <summary>
    /// Monotonically-increasing position assigned at join time.
    /// Primary sort key for FIFO matching — lower position = earlier in queue.
    /// </summary>
    public int Position { get; set; }

    // ── Navigation to claimed appointment (AC-3) ──────────────────────────────

    /// <summary>Populated after the patient successfully claims the offered slot.</summary>
    public Guid? AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }
}
