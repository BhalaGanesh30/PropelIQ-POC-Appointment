using PropelIQ.Modules.Scheduling.Domain.Events;

namespace PropelIQ.Modules.Scheduling.Application.Waitlist;

/// <summary>
/// Builds and dispatches the preferred-slot availability alert (US_030 AC-1).
///
/// Responsibilities:
///   1. Load patient contact data and channel preferences from the database.
///   2. Generate and persist an HMAC-signed claim token (AC-3 / OWASP A01).
///   3. Dispatch the alert via each enabled channel (email and/or SMS) within
///      the 5-minute SLA from when the slot was made available.
/// </summary>
public interface ISlotAlertService
{
    /// <summary>
    /// Dispatch the slot availability alert for the event received from the
    /// <see cref="System.Threading.Channels.Channel{T}"/> pipeline.
    /// </summary>
    /// <param name="evt">
    ///   The <see cref="SlotOfferedEvent"/> published by
    ///   <c>WaitlistService.MatchSlotToWaitlistAsync</c>.
    /// </param>
    /// <param name="ct">Propagated cancellation token (host shutdown).</param>
    Task DispatchAlertAsync(SlotOfferedEvent evt, CancellationToken ct);
}
