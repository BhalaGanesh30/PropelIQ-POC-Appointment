using PropelIQ.Modules.Scheduling.Application.Appointments.Dto;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Data access abstraction for the appointment history API (US_025).
///
/// AC-1 / AC-2 / AC-3: <see cref="GetFilteredAsync"/> returns paginated, filtered,
/// date-descending results within 500 ms p95 via the composite index on
/// (PatientId, ScheduledAt DESC, Status).
///
/// AC-4: <see cref="StreamFilteredAsync"/> streams all filtered records without a
/// pagination limit so the PDF export contains the complete result set regardless
/// of how many appointments the patient has.
/// </summary>
public interface IAppointmentHistoryRepository
{
    /// <summary>
    /// Returns a paginated, filtered list of appointments for the given user.
    /// Resolves the auth user ID to the corresponding patient record internally.
    /// Results are sorted by <c>ScheduledAt</c> descending (AC-1).
    /// </summary>
    Task<(List<Appointment> Items, int TotalCount)> GetFilteredAsync(
        Guid userId,
        AppointmentHistoryFilter filter,
        CancellationToken ct);

    /// <summary>
    /// Streams all filtered appointments without pagination for PDF export (AC-4).
    /// Uses <c>AsAsyncEnumerable()</c> so rows are yielded incrementally — the query
    /// stays within the 5-second PDF generation budget even for large datasets.
    /// </summary>
    IAsyncEnumerable<Appointment> StreamFilteredAsync(
        Guid userId,
        AppointmentHistoryFilter filter,
        CancellationToken ct);
}
