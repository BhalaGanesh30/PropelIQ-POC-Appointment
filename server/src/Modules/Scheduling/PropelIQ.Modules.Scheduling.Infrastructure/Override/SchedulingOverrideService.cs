using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Override.Dto;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Override;

/// <summary>
/// Implements <see cref="ISchedulingOverrideService"/> for EP-004 US_034.
///
/// AC-2: Validates that the stated constraint actually applies to the appointment
///       (prevents fabricated override payloads), executes the scheduling action,
///       and persists an immutable AuditRecord — all within a single DB transaction
///       per DR-002.
///
/// AC-3: Reason validation is enforced at the DTO layer ([Required, MinLength(1),
///       MaxLength(500)]); the service receives a pre-validated request.
///
/// Edge Case 1: Constraint validation returns 400 when the stated constraint does
///              not actually apply to the appointment.
/// Edge Case 2: [Authorize(Roles="Staff,Admin")] on the controller ensures Patient
///              callers never reach this service (→ 403 returned by ASP.NET Core).
///
/// NFR-010: Audit write is append-only; records are never mutated after creation.
/// NFR-011: OTel span emitted per override call.
/// </summary>
public sealed class SchedulingOverrideService : ISchedulingOverrideService
{
    // ── OTel (NFR-011) ─────────────────────────────────────────────────────────
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Scheduling.SchedulingOverrideService");

    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;
    private readonly ILogger<SchedulingOverrideService> _logger;

    public SchedulingOverrideService(
        AppDbContext db,
        IAuditService auditService,
        ILogger<SchedulingOverrideService> logger)
    {
        _db           = db;
        _auditService = auditService;
        _logger       = logger;
    }

    /// <inheritdoc />
    public async Task<SchedulingOverrideResponse> ExecuteOverrideAsync(
        SchedulingOverrideRequest request,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("scheduling.override.execute");
        activity?.SetTag("appointment.id",   request.AppointmentId.ToString());
        activity?.SetTag("constraint.type",  request.ConstraintType.ToString());
        activity?.SetTag("action",           request.Action.ToString());
        activity?.SetTag("staff.user.id",    staffUserId.ToString());

        // ── 1. Load appointment ───────────────────────────────────────────────
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct);

        if (appointment is null)
        {
            _logger.LogWarning(
                "Override attempted on non-existent appointment {AppointmentId} by staff {StaffUserId}",
                request.AppointmentId, staffUserId);
            throw new KeyNotFoundException(
                $"Appointment '{request.AppointmentId}' was not found.");
        }

        // ── 2. Validate that the stated constraint actually applies (AC-2) ─────
        // Prevents fabricated override payloads from bypassing real constraints.
        ValidateConstraintApplies(request.AppointmentId, request.ConstraintType, appointment.ScheduledAt);

        // ── 3. Begin transaction for atomicity (DR-002) ───────────────────────
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var overrideId = Guid.NewGuid();

            // ── 4. Execute the scheduling action ─────────────────────────────
            ApplyAction(appointment, request.Action);

            // ── 5. Write immutable audit record (NFR-010, AC-2) ───────────────
            // IAuditService.LogOverrideAsync calls SaveChangesAsync internally.
            // Because we're inside an EF transaction, both the appointment update
            // and the audit insert are committed together in step 6.
            var auditPayload = new OverrideAuditPayload
            {
                AppointmentId    = request.AppointmentId,
                ConstraintType   = request.ConstraintType.ToString(),
                Reason           = request.Reason.Trim(),
                Action           = request.Action.ToString(),
                OverrideRecordId = overrideId,
            };

            var auditRecordId = await _auditService.LogOverrideAsync(auditPayload, staffUserId, ct);

            // ── 6. Commit ─────────────────────────────────────────────────────
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Override applied: appointment={AppointmentId} constraint={ConstraintType} " +
                "action={Action} by staff={StaffUserId} overrideId={OverrideId}",
                request.AppointmentId, request.ConstraintType,
                request.Action, staffUserId, overrideId);

            activity?.SetTag("override.id",     overrideId.ToString());
            activity?.SetTag("audit.record.id", auditRecordId.ToString());

            return new SchedulingOverrideResponse
            {
                OverrideId    = overrideId,
                AuditRecordId = auditRecordId,
                Status        = "Applied",
                AppointmentId = request.AppointmentId,
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Validates that the stated constraint actually applies to the appointment.
    /// Throws <see cref="InvalidOperationException"/> (→ 400) when it does not.
    ///
    /// Time-window constraints are evaluated relative to <c>UtcNow</c>.
    /// Structural constraints (SlotConflict, CapacityExceeded) cannot be
    /// re-evaluated from appointment data alone — they are accepted as stated
    /// by the trusted staff caller.
    /// </summary>
    private static void ValidateConstraintApplies(
        Guid appointmentId,
        SchedulingConstraintType constraintType,
        DateTimeOffset scheduledAt)
    {
        var now = DateTimeOffset.UtcNow;

        switch (constraintType)
        {
            case SchedulingConstraintType.CancellationWithin24Hours:
            case SchedulingConstraintType.RescheduleWithin24Hours:
                // Constraint applies only when the appointment is within 24 hours.
                if (scheduledAt - now >= TimeSpan.FromHours(24))
                {
                    throw new InvalidOperationException(
                        $"Constraint '{constraintType}' does not apply to appointment " +
                        $"'{appointmentId}': the appointment is more than 24 hours away.");
                }
                break;

            // SlotConflict and CapacityExceeded are contextual — accepted as stated.
            case SchedulingConstraintType.SlotConflict:
            case SchedulingConstraintType.CapacityExceeded:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown constraint type '{constraintType}'.");
        }
    }

    /// <summary>
    /// Applies the override action to the appointment entity.
    /// For <see cref="OverrideAction.Cancel"/> the appointment is cancelled immediately.
    /// For <see cref="OverrideAction.Reschedule"/> and <see cref="OverrideAction.ForceBook"/>
    /// the override is recorded; the caller submits the follow-up action separately.
    /// </summary>
    private static void ApplyAction(
        Domain.Entities.Appointment appointment,
        OverrideAction action)
    {
        switch (action)
        {
            case OverrideAction.Cancel:
                appointment.Status = "Cancelled";
                appointment.QueueState = "Cancelled";
                break;

            // Reschedule / ForceBook: override intent is recorded via the audit log.
            // The client calls the relevant endpoint with the override context.
            case OverrideAction.Reschedule:
            case OverrideAction.ForceBook:
                // No state mutation — the audit record is the authorisation token.
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }
}
