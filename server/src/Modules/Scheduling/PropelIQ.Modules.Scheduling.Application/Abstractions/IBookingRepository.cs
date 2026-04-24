using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Data access abstraction for booking operations.
/// Implemented in Infrastructure by BookingRepository with EF Core transactions
/// and optimistic concurrency via AppointmentSlot.RowVersion (AC-1, AC-4).
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Returns the slot by ID if it is still available (future, not fully booked).
    /// The returned entity is change-tracked so the RowVersion check fires on
    /// <see cref="CreateBookingAsync"/>.
    /// </summary>
    Task<AppointmentSlot?> GetSlotForBookingAsync(Guid slotId, CancellationToken ct);

    /// <summary>
    /// Returns the earliest available slot after <paramref name="afterTime"/>,
    /// optionally filtered by appointment type.
    /// Used to populate the <c>nextAvailableSlot</c> suggestion on HTTP 409 (AC-4).
    /// </summary>
    Task<AppointmentSlot?> GetNextAvailableSlotAsync(
        DateTimeOffset afterTime,
        AppointmentType? type,
        CancellationToken ct);

    /// <summary>
    /// Atomically increments <see cref="AppointmentSlot.CurrentBookings"/> and
    /// persists the new <see cref="Appointment"/> in a single SaveChanges call.
    /// Throws <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// when the slot's RowVersion has changed since it was read (AC-4).
    /// </summary>
    Task<Appointment> CreateBookingAsync(
        Appointment appointment,
        AppointmentSlot slot,
        CancellationToken ct);

    // ── Cancel / Reschedule ───────────────────────────────────────────────────

    /// <summary>Returns a tracked appointment by ID (staff access — no patient filter).</summary>
    Task<Appointment?> GetAppointmentAsync(Guid appointmentId, CancellationToken ct);

    /// <summary>
    /// Returns a tracked appointment by ID scoped to the specified patient.
    /// Returns <see langword="null"/> when the appointment does not exist or belongs
    /// to a different patient — enforces ownership at the repository level.
    /// </summary>
    Task<Appointment?> GetAppointmentForPatientAsync(
        Guid appointmentId, Guid patientId, CancellationToken ct);

    /// <summary>
    /// Returns a change-tracked slot by ID regardless of availability.
    /// Used by <see cref="RescheduleBookingAsync"/> to hold the old slot for decrement.
    /// </summary>
    Task<AppointmentSlot?> GetTrackedSlotAsync(Guid slotId, CancellationToken ct);

    /// <summary>
    /// Persists the appointment's mutated state (e.g. Status = Cancelled).
    /// Slot release is handled separately via <see cref="ReleaseSlotAsync"/> to
    /// enable independent Polly retry on slot release failures (edge case).
    /// </summary>
    Task SaveAppointmentAsync(Appointment appointment, CancellationToken ct);

    /// <summary>
    /// Decrements <see cref="AppointmentSlot.CurrentBookings"/> and saves.
    /// Called independently so Polly can retry only the slot release on failure (edge case).
    /// </summary>
    Task ReleaseSlotAsync(Guid slotId, CancellationToken ct);

    /// <summary>
    /// Atomically decrements the old slot, increments the new slot, updates the
    /// appointment fields, and persists in a single SaveChanges call (AC-2).
    /// Throws <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// if either slot's RowVersion changed since load.
    /// </summary>
    Task<Appointment> RescheduleBookingAsync(
        Appointment appointment,
        AppointmentSlot oldSlot,
        AppointmentSlot newSlot,
        CancellationToken ct);

    /// <summary>
    /// Appends an immutable audit entry for staff override actions (AC-4, DR-005).
    /// </summary>
    Task CreateAuditEntryAsync(AppointmentAuditEntry entry, CancellationToken ct);
}
