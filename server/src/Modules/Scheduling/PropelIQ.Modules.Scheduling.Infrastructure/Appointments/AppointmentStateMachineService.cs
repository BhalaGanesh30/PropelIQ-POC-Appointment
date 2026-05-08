using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.Scheduling.Domain.Exceptions;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Appointments;

/// <summary>
/// Implements the US_032 check-in state machine over EF Core + AuditRecord.
///
/// Transition rules (Edge Case 1: any other combination throws <see cref="InvalidStateTransitionException"/>):
///   CheckIn       Scheduled | NotQueued         → Arrived
///   StartVisit    Arrived   | Waiting            → InProgress
///   CompleteVisit InProgress                     → Completed
///   NoShow        Scheduled | Arrived | NotQueued | Waiting → NoShow
///
/// AC-1: CheckIn records <see cref="Appointment.ArrivedAt"/>.
/// AC-2: StartVisit records <see cref="Appointment.VisitStartedAt"/>.
/// AC-3: CompleteVisit records <see cref="Appointment.VisitEndedAt"/>.
/// AC-4: NoShow writes an <c>AuditRecord</c> with the acting staff member ID.
///
/// NFR-010: Every transition writes an immutable AuditRecord — append-only.
/// NFR-011: OTel span <c>appointment.state.transition</c> emitted per call.
/// </summary>
public sealed class AppointmentStateMachineService : IAppointmentStateMachineService
{
    // ── OTel (NFR-011) ─────────────────────────────────────────────────────────
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Scheduling.AppointmentStateMachine");

    // ── Transition map: action → (valid from-states[], to-state string) ────────
    // Stored as strings to match Appointment.QueueState (string column).
    // "NotQueued" is the Appointment default; "Waiting" is the pre-US_032 legacy value.
    private static readonly IReadOnlyDictionary<AppointmentStateAction, (string[] ValidFrom, string To)> Transitions =
        new Dictionary<AppointmentStateAction, (string[] ValidFrom, string To)>
        {
            [AppointmentStateAction.CheckIn] =
                (["Scheduled", "NotQueued"], nameof(QueueState.Arrived)),

            [AppointmentStateAction.StartVisit] =
                (["Arrived", "Waiting"], nameof(QueueState.InProgress)),

            [AppointmentStateAction.CompleteVisit] =
                ([nameof(QueueState.InProgress)], nameof(QueueState.Completed)),

            [AppointmentStateAction.NoShow] =
                (["Scheduled", "Arrived", "NotQueued", "Waiting"], nameof(QueueState.NoShow)),
        };

    private readonly AppDbContext _db;
    private readonly ILogger<AppointmentStateMachineService> _logger;

    public AppointmentStateMachineService(
        AppDbContext db,
        ILogger<AppointmentStateMachineService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Appointment> TransitionAsync(
        Guid appointmentId,
        AppointmentStateAction action,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("appointment.state.transition");
        activity?.SetTag("appointment.id", appointmentId.ToString());
        activity?.SetTag("action", action.ToString());

        // ── 1. Load appointment ───────────────────────────────────────────────
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);

        if (appointment is null)
        {
            _logger.LogWarning(
                "State transition {Action} attempted on non-existent appointment {AppointmentId}",
                action, appointmentId);
            throw new KeyNotFoundException(
                $"Appointment '{appointmentId}' was not found.");
        }

        // ── 2. Validate current state ─────────────────────────────────────────
        var (validFrom, toState) = Transitions[action];
        var currentState = appointment.QueueState;

        if (!validFrom.Contains(currentState, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Invalid state transition: action={Action} currentState={CurrentState} appointmentId={AppointmentId}",
                action, currentState, appointmentId);
            throw new InvalidStateTransitionException(action.ToString(), currentState);
        }

        activity?.SetTag("from_state", currentState);
        activity?.SetTag("to_state", toState);

        // ── 3. Apply transition + record timestamp ────────────────────────────
        var now = DateTimeOffset.UtcNow;
        appointment.QueueState = toState;

        switch (action)
        {
            case AppointmentStateAction.CheckIn:
                appointment.ArrivedAt = now;           // AC-1
                break;
            case AppointmentStateAction.StartVisit:
                appointment.VisitStartedAt = now;      // AC-2
                break;
            case AppointmentStateAction.CompleteVisit:
                appointment.VisitEndedAt = now;        // AC-3
                break;
            // NoShow: no timestamp column per task spec; audit record is the evidence.
        }

        // ── 4. Write immutable audit record (NFR-010, AC-4) ───────────────────
        _db.AuditRecords.Add(new AuditRecord
        {
            EventType = "AppointmentStateTransition",
            ActorUserId = staffUserId,
            TargetEntityId = appointmentId,
            TargetEntityType = "Appointment",
            OccurredAt = now,
            Details = new AuditDetails
            {
                ChangeDescription =
                    $"QueueState transitioned from '{currentState}' to '{toState}' by staff {staffUserId}.",
                Metadata = new Dictionary<string, string>
                {
                    ["fromState"]       = currentState,
                    ["toState"]         = toState,
                    ["staffUserId"]     = staffUserId.ToString(),
                    ["transitionedAt"]  = now.ToString("O"),
                    ["action"]          = action.ToString(),
                },
            },
        });

        // ── 5. Persist atomically (EF Core implicit transaction) ──────────────
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Appointment {AppointmentId} transitioned {From} → {To} by staff {StaffUserId}",
            appointmentId, currentState, toState, staffUserId);

        return appointment;
    }
}
