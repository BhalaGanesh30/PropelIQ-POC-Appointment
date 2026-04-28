using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Persistence abstraction for <see cref="ReminderEvent"/> CRUD operations.
/// Implemented in the Infrastructure layer via EF Core.
/// </summary>
public interface IReminderEventRepository
{
    /// <summary>
    /// Persists a batch of reminder events, skipping any whose IdempotencyKey
    /// already exists (safe for retries and duplicate event delivery).
    /// </summary>
    Task AddRangeAsync(
        IEnumerable<ReminderEvent> events,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk-updates all Pending reminders for the given appointment to Cancelled
    /// in a single UPDATE statement (AC-3, AC-4).
    /// </summary>
    Task CancelPendingByAppointmentAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a single reminder event by its primary key, or <c>null</c>
    /// if not found. Used for double-click protection in the confirm/cancel flow.
    /// </summary>
    Task<ReminderEvent?> GetByIdAsync(
        Guid reminderId,
        CancellationToken ct = default);

    /// <summary>
    /// Records the patient's confirmation/cancellation response on the reminder
    /// (AC-3: "Confirmed" or "Cancelled") for audit and double-click detection.
    /// </summary>
    Task RecordConfirmationResponseAsync(
        Guid reminderId,
        string response,
        CancellationToken ct = default);
}
