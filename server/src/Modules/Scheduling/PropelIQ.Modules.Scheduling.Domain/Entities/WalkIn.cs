using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// Temporary walk-in record created when a patient arrives at the clinic without
/// a prior booking (EP-004 US_033).
///
/// AC-1: Persisted in WalkIns table; linked to an Appointment with
///       AppointmentType=WalkIn and QueueState=Waiting for queue insertion.
/// AC-2: <see cref="IsConverted"/> flips to true once a full patient account
///       is created and linked via WalkinService.ConvertWalkinAsync.
/// AC-4: <see cref="PatientId"/> is set when linked to an existing patient
///       (bypass conversion flow) or after conversion.
/// </summary>
public sealed class WalkIn : BaseEntity
{
    /// <summary>Patient full name as entered by staff at the front desk.</summary>
    public required string PatientName { get; set; }

    /// <summary>Optional contact phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Reason for today's visit (required).</summary>
    public required string VisitReason { get; set; }

    /// <summary>
    /// FK to <c>Patients</c> table.
    /// Null for purely anonymous walk-ins until linked or converted.
    /// Set immediately when <c>ExistingPatientId</c> is provided at creation.
    /// </summary>
    public Guid? PatientId { get; set; }

    /// <summary>FK to the <c>Appointments</c> queue entry for this walk-in.</summary>
    public Guid? AppointmentId { get; set; }

    /// <summary>
    /// True once a full patient account (User + Patient record) has been
    /// created from this walk-in's demographics via ConvertWalkinAsync.
    /// </summary>
    public bool IsConverted { get; set; } = false;

    /// <summary>Staff user who registered this walk-in (audit FK).</summary>
    public required Guid CreatedByUserId { get; set; }
}
