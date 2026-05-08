using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Owns all state transitions for the appointment check-in workflow (EP-004 US_032).
///
/// This is the <em>sole</em> component permitted to mutate
/// <see cref="Appointment.QueueState"/> and the corresponding
/// timestamp columns (<c>ArrivedAt</c>, <c>VisitStartedAt</c>, <c>VisitEndedAt</c>).
///
/// Each successful transition writes an immutable <c>AuditRecord</c> row per NFR-010.
/// </summary>
public interface IAppointmentStateMachineService
{
    /// <summary>
    /// Applies <paramref name="action"/> to the appointment, validates the current state,
    /// records the timestamp, writes an audit record, and persists both changes atomically.
    /// </summary>
    /// <param name="appointmentId">PK of the appointment to transition.</param>
    /// <param name="action">The transition action (AC-1 through AC-4).</param>
    /// <param name="staffUserId">ID of the staff member performing the action (NFR-010 audit).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="Appointment"/> entity after the transition.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no appointment with the given ID exists.</exception>
    /// <exception cref="Domain.Exceptions.InvalidStateTransitionException">
    /// Thrown when the appointment's current state does not allow the requested action (Edge Case 1).
    /// No DB write occurs.
    /// </exception>
    Task<Appointment> TransitionAsync(
        Guid appointmentId,
        AppointmentStateAction action,
        Guid staffUserId,
        CancellationToken ct = default);
}
