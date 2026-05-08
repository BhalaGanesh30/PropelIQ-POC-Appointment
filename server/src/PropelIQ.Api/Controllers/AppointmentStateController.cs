using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Api.Models.DTOs;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Queue.Dto;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.Scheduling.Domain.Exceptions;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Staff appointment state-machine API (EP-004 US_032).
///
/// PATCH /api/v1/appointments/{id}/state — drives the check-in workflow
/// (Scheduled → Arrived → InProgress → Completed / NoShow) and returns the
/// updated queue entry reflecting the new state.
///
/// FR-SO-002: Restricted to Staff and Admin roles.
/// NFR-010: Every successful transition writes an immutable AuditRecord via the
///          state machine service.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[ApiController]
[Route("api/v1/appointments")]
[Produces("application/json")]
public sealed class AppointmentStateController : ControllerBase
{
    private readonly IAppointmentStateMachineService _stateMachine;
    private readonly IWaitTimeEstimationService _waitTimeService;

    public AppointmentStateController(
        IAppointmentStateMachineService stateMachine,
        IWaitTimeEstimationService waitTimeService)
    {
        _stateMachine = stateMachine;
        _waitTimeService = waitTimeService;
    }

    /// <summary>
    /// Applies a state transition to the specified appointment.
    /// </summary>
    /// <param name="id">PK of the appointment to transition.</param>
    /// <param name="request">Transition action: CheckIn, StartVisit, CompleteVisit, or NoShow.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="QueueEntryDto"/> after the transition.</returns>
    /// <response code="200">Transition applied; updated queue entry returned.</response>
    /// <response code="400">Invalid <paramref name="request.Action"/> value (unrecognised enum string).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    /// <response code="404">No appointment exists with the given <paramref name="id"/>.</response>
    /// <response code="422">The appointment's current state does not permit this transition (Edge Case 1).</response>
    [HttpPatch("{id:guid}/state")]
    [ProducesResponseType(typeof(QueueEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TransitionState(
        [FromRoute] Guid id,
        [FromBody] TransitionStateRequest request,
        CancellationToken ct)
    {
        // ── Resolve staff identity from JWT ───────────────────────────────────
        var staffUserIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        if (staffUserIdRaw is null || !Guid.TryParse(staffUserIdRaw, out var staffUserId))
            return Unauthorized(new { message = "Staff identity could not be resolved from token." });

        // ── Delegate to state machine ─────────────────────────────────────────
        try
        {
            var appointment = await _stateMachine.TransitionAsync(
                id,
                request.Action,
                staffUserId,
                ct);

            // ── Project to QueueEntryDto ──────────────────────────────────────
            // Queue position is unknown here (single-row context); use 0 so the
            // estimated wait reflects the minimum service duration for this type.
            // The dashboard polling will recompute accurate positions on next refresh.
            var now = DateTimeOffset.UtcNow;
            var referenceTime = appointment.ArrivedAt ?? appointment.ScheduledAt;
            var estimatedWait = _waitTimeService.CalculateEstimatedWaitMinutes(
                queuePosition: 0,
                appointmentTypeCode: appointment.AppointmentType);
            var actualWait = (int)Math.Max(0, (now - referenceTime).TotalMinutes);
            var isOverdue = _waitTimeService.IsOverdue(appointment.ArrivedAt, estimatedWait);

            var status = Enum.TryParse<QueueState>(appointment.QueueState, out var parsed)
                ? parsed
                : QueueState.Waiting;

            var dto = new QueueEntryDto
            {
                AppointmentId        = appointment.Id,
                PatientId            = appointment.PatientId,
                // PatientName not available without a patient join — return a
                // placeholder; the dashboard refreshes the full row on next poll.
                PatientName          = string.Empty,
                AppointmentType      = appointment.AppointmentType,
                Status               = status,
                ArrivedAt            = appointment.ArrivedAt,
                ScheduledAt          = appointment.ScheduledAt,
                EstimatedWaitMinutes = estimatedWait,
                ActualWaitMinutes    = actualWait,
                IsOverdue            = isOverdue,
            };

            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            // NotFoundException — appointment does not exist.
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidStateTransitionException ex)
        {
            // Edge Case 1: invalid state transition — HTTP 422 with descriptive message.
            return UnprocessableEntity(new { message = ex.Message });
        }
    }
}
