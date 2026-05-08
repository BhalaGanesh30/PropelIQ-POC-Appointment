using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// Booking record created when a patient confirms a slot reservation.
/// Extends the base Appointment with atomic slot reference, intake linkage,
/// and a cryptographically-generated confirmation code.
///
/// AC-1: AtomicReservation — SlotId FK + RowVersion on AppointmentSlot ensure
///       exactly-once booking even under concurrent requests.
/// AC-4: DbUpdateConcurrencyException signals a race; the slot's RowVersion
///       mismatch causes the second booking to receive HTTP 409.
/// NFR-010: CreatedAt / UpdatedAt inherited from BaseEntity for audit.
/// </summary>
public sealed class Appointment : BaseEntity
{
    public required Guid PatientId { get; set; }

    /// <summary>
    /// Nullable — populated from the slot's ProviderId when known;
    /// may be assigned later for walk-in or external referrals.
    /// </summary>
    public Guid? StaffUserId { get; set; }

    public required DateTimeOffset ScheduledAt { get; set; }
    public required int DurationMinutes { get; set; }
    public required string AppointmentType { get; set; }

    /// <summary>
    /// Lifecycle status stored as a string for readability in the DB.
    /// Use <see cref="AppointmentStatus"/> enum values via <c>.ToString()</c>.
    /// </summary>
    public string Status { get; set; } = AppointmentStatus.Confirmed.ToString();

    public string QueueState { get; set; } = "NotQueued";

    // ── Queue timing fields (EP-004 US_031 task_004) ──────────────────────────

    /// <summary>
    /// UTC timestamp when the patient checked in on the day of the visit.
    /// Null until the check-in workflow (post-task_004) sets this field.
    /// Required by <see cref="IWaitTimeEstimationService.IsOverdue"/> for AC-3.
    /// </summary>
    public DateTimeOffset? ArrivedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the clinician started the visit (patient called in).
    /// Null until the visit-start transition is implemented.
    /// </summary>
    public DateTimeOffset? VisitStartedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the visit was marked complete.
    /// Null until the visit-end transition is implemented.
    /// </summary>
    public DateTimeOffset? VisitEndedAt { get; set; }

    // ── Booking-specific fields ───────────────────────────────────────────────

    /// <summary>Slot that was atomically reserved for this appointment (AC-1).</summary>
    public Guid? SlotId { get; set; }

    /// <summary>Finalized intake record attached at booking time (US_020).</summary>
    public Guid? IntakeRecordId { get; set; }

    /// <summary>Cryptographically random 8-character alphanumeric code (DR-002).</summary>
    public string? ConfirmationCode { get; set; }

    /// <summary>True once the PDF / QR / ICS artifacts have been generated (task_002).</summary>
    public bool ArtifactsGenerated { get; set; } = false;

    /// <summary>Storage path of the generated PDF confirmation (task_002).</summary>
    public string? PdfStoragePath { get; set; }

    /// <summary>Storage path of the generated QR code PNG (task_002).</summary>
    public string? QrCodeStoragePath { get; set; }

    /// <summary>Storage path of the generated ICS calendar file (task_002).</summary>
    public string? IcsStoragePath { get; set; }

    /// <summary>
    /// RFC 5545 SEQUENCE counter — incremented on each reschedule so calendar clients
    /// recognise updates rather than creating duplicate events (AC-3, US_024 task_001).
    /// </summary>
    public int SequenceNumber { get; set; } = 0;

    /// <summary>UTC timestamp when all artifacts were successfully generated.</summary>
    public DateTimeOffset? ArtifactsGeneratedAt { get; set; }

    /// <summary>True once the confirmation email with artifacts has been sent (AC-2).</summary>
    public bool EmailSent { get; set; } = false;

    /// <summary>Number of email delivery attempts (retried up to 3 times — edge case).</summary>
    public int EmailRetryCount { get; set; } = 0;

    /// <summary>UTC timestamp when the booking was committed.</summary>
    public DateTimeOffset BookedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Denormalized provider name from the slot for display without a join.</summary>
    public string? ProviderName { get; set; }

    /// <summary>Denormalized location from the slot for display without a join.</summary>
    public string? Location { get; set; }

    public WaitlistEntry? WaitlistEntry { get; set; }
    public ICollection<ReminderEvent> ReminderEvents { get; set; } = [];

    // ── No-show risk score (US_028 task_001) ─────────────────────────────────

    /// <summary>
    /// Risk classification assigned by the no-show risk scoring service.
    /// One of: Low, Medium, High, Unknown.
    /// Null when scoring has never been performed.
    /// </summary>
    public string? RiskLevel { get; set; }

    /// <summary>Model confidence in the risk classification (0.0–1.0).</summary>
    public double? RiskConfidence { get; set; }

    /// <summary>
    /// JSONB-serialised list of <c>RiskFeatureContribution</c> records explaining
    /// why the risk label was assigned (AIR-004 explainability requirement).
    /// </summary>
    public string? RiskFeatures { get; set; }

    /// <summary>
    /// UTC timestamp when the risk score was last calculated.
    /// Used to determine staleness (24-hour TTL) before recalculation.
    /// </summary>
    public DateTimeOffset? RiskScoredAt { get; set; }
    // ── Staff-assisted booking (EP-004 US_035) ────────────────────────────────

    /// <summary>
    /// UUID of the staff member who created this booking on behalf of the patient (AC-2).
    /// Null for patient self-bookings. Populated only when the booking originates from
    /// POST /api/v1/staff-bookings.
    /// </summary>
    public Guid? CreatedByStaffId { get; set; }
}

