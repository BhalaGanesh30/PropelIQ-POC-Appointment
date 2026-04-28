namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Canonical reminder offset definitions and idempotency key builder.
/// FR-RN-001: reminders fire at 7 days, 2 days, 1 day, and 2 hours before appointment start.
/// </summary>
public static class ReminderOffsets
{
    /// <summary>All configured reminder offsets in descending order (furthest first).</summary>
    public static readonly TimeSpan[] All =
    [
        TimeSpan.FromDays(7),
        TimeSpan.FromDays(2),
        TimeSpan.FromDays(1),
        TimeSpan.FromHours(2)
    ];

    /// <summary>
    /// Builds a composite idempotency key to prevent duplicate <c>ReminderEvent</c> rows
    /// on retries or duplicate event delivery.
    /// Format: {AppointmentId}_{OffsetMinutes}_{Channel}
    /// </summary>
    public static string BuildIdempotencyKey(
        Guid appointmentId,
        TimeSpan offset,
        string channel)
        => $"{appointmentId}_{(int)offset.TotalMinutes}_{channel}";
}
