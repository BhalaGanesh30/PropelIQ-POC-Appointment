using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Tracks a patient-initiated request for a disclosure of all data-access events
/// on their records within a date range (US_057, AC-2, AC-3, FR-AC-002).
///
/// State machine:
///   Submitted → Compiling (DisclosureCompilationWorker picks it up)
///   Compiling → PendingReview (compilation complete)
///   PendingReview → Approved | Rejected (staff review)
///   Approved → Delivered (download link sent to patient)
/// </summary>
public sealed class DisclosureRequest : BaseEntity
{
    /// <summary>Patient who submitted the request.</summary>
    public required Guid PatientId { get; set; }

    /// <summary>Inclusive start of the requested access date range (UTC).</summary>
    public required DateTimeOffset FromDateUtc { get; set; }

    /// <summary>Inclusive end of the requested access date range (UTC).</summary>
    public required DateTimeOffset ToDateUtc { get; set; }

    /// <summary>Current state-machine status. Starts at Submitted.</summary>
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Submitted;

    /// <summary>UTC timestamp when the compilation job completed.</summary>
    public DateTimeOffset? CompiledAt { get; set; }

    /// <summary>User ID of the staff member who reviewed the request.</summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>UTC timestamp when the request was reviewed.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Optional reviewer notes (max 1000 chars).</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>UTC timestamp when the disclosure was delivered to the patient.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>Delivery channel used: "Email" or "SecureDownload".</summary>
    public string? DeliveryMethod { get; set; }

    /// <summary>FK to the compiled disclosure report (null until compilation completes).</summary>
    public Guid? ReportId { get; set; }

    /// <summary>Navigation to the compiled report.</summary>
    public DisclosureReport? Report { get; set; }

    // ── State transitions ────────────────────────────────────────────────────

    /// <summary>Transitions to the specified status and marks the entity as updated.</summary>
    public void Transition(DisclosureStatus newStatus)
    {
        Status = newStatus;
        MarkUpdated();
    }
}
