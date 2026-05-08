using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Api.Models.DTOs;

/// <summary>
/// Request body for <c>PATCH /api/v1/appointments/{id}/state</c> (EP-004 US_032).
///
/// <see cref="Action"/> is bound from a case-insensitive string by ASP.NET Core's
/// built-in enum model binder.  An unrecognised value causes a HTTP 400 automatically
/// via <c>[ApiController]</c> (Edge Case 1 — invalid action).
/// </summary>
public sealed record TransitionStateRequest
{
    /// <summary>
    /// The transition to apply.  Valid values: <c>CheckIn</c>, <c>StartVisit</c>,
    /// <c>CompleteVisit</c>, <c>NoShow</c>.
    /// </summary>
    public required AppointmentStateAction Action { get; init; }
}
