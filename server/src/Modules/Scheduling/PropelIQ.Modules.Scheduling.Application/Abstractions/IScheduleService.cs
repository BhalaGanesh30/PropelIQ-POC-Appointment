using PropelIQ.Modules.Scheduling.Application.Schedule.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Daily schedule retrieval and drag-and-drop reschedule service (EP-004 US_036 FR-SO-006).
///
/// AC-1: Returns all appointments for a date with patient names, types, and durations.
/// AC-2: Reschedule validates conflicts, persists the new time, and writes an immutable
///       audit record capturing the override reason and staff identity.
/// AC-4: Schedule data is served within NFR-002 (500ms p95) via 30-second Redis TTL.
/// Edge Case 1: Throws <see cref="ScheduleConflictException"/> when the target time
///              slot is occupied; controller maps this to HTTP 409.
/// Edge Case 2: Returns empty <see cref="DailyScheduleResponseDto.Entries"/> list
///              for dates with no appointments.
/// </summary>
public interface IScheduleService
{
    /// <summary>
    /// Returns all appointments for the given calendar date, sorted by start time (AC-1).
    /// Result is served from Redis on cache hit (30-second TTL) — AC-4.
    /// </summary>
    /// <param name="date">Calendar date for which to load appointments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Wrapper DTO with entries sorted by <c>StartTime ASC</c>.
    /// Returns empty entries list for dates with no appointments (Edge Case 2).
    /// </returns>
    Task<DailyScheduleResponseDto> GetDailyScheduleAsync(
        DateOnly date,
        CancellationToken ct = default);

    /// <summary>
    /// Reschedules an appointment to a new start time, validates for conflicts,
    /// creates an immutable audit record, and invalidates the Redis cache (AC-2).
    /// </summary>
    /// <param name="request">Reschedule payload with new time and mandatory override reason.</param>
    /// <param name="staffUserId">UUID of the authenticated staff member (from JWT <c>sub</c> claim).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Old/new times and the audit record UUID.</returns>
    /// <exception cref="KeyNotFoundException">Appointment not found.</exception>
    /// <exception cref="ScheduleConflictException">
    /// Target time slot is occupied by another appointment (Edge Case 1).
    /// Inspect <see cref="ScheduleConflictException.ConflictingEntry"/> for details.
    /// </exception>
    Task<RescheduleResponseDto> RescheduleAsync(
        RescheduleRequestDto request,
        Guid staffUserId,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown by <see cref="IScheduleService.RescheduleAsync"/> when the target time slot
/// is already occupied by another appointment (Edge Case 1).
/// The controller maps this to HTTP 409 Conflict.
/// </summary>
public sealed class ScheduleConflictException : Exception
{
    /// <summary>Details of the appointment that occupies the target time slot.</summary>
    public DailyScheduleEntryDto ConflictingEntry { get; }

    public ScheduleConflictException(DailyScheduleEntryDto conflictingEntry)
        : base($"The target time slot is occupied by appointment {conflictingEntry.AppointmentId}.")
    {
        ConflictingEntry = conflictingEntry;
    }
}
