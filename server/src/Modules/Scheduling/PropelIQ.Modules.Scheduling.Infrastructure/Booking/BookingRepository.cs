using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// EF Core implementation of <see cref="IBookingRepository"/>.
///
/// AC-1: <see cref="CreateBookingAsync"/> increments <see cref="AppointmentSlot.CurrentBookings"/>
///       and saves the new <see cref="Appointment"/> in a single transaction.
/// AC-4: Because the slot is loaded with change-tracking (no AsNoTracking) and configured
///       with IsRowVersion(), EF Core appends a WHERE xmin = &lt;original&gt; predicate.
///       When a concurrent request modifies the row first the predicate matches zero rows
///       and EF throws <see cref="DbUpdateConcurrencyException"/>.
/// </summary>
public sealed class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<AppointmentSlot?> GetSlotForBookingAsync(
        Guid slotId, CancellationToken ct)
    {
        // Must NOT use AsNoTracking — the tracked entity carries the RowVersion
        // value needed for the optimistic concurrency WHERE clause (AC-4).
        return await _context.AppointmentSlots
            .FirstOrDefaultAsync(
                s => s.Id == slotId
                  && s.StartTime > DateTimeOffset.UtcNow
                  && s.CurrentBookings < s.MaxCapacity,
                ct);
    }

    /// <inheritdoc />
    public async Task<AppointmentSlot?> GetNextAvailableSlotAsync(
        DateTimeOffset afterTime,
        AppointmentType? type,
        CancellationToken ct)
    {
        var query = _context.AppointmentSlots
            .AsNoTracking()
            .Where(s => s.StartTime > afterTime
                     && s.CurrentBookings < s.MaxCapacity);

        if (type.HasValue)
            query = query.Where(s => s.Type == type.Value);

        return await query
            .OrderBy(s => s.StartTime)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Appointment> CreateBookingAsync(
        Appointment appointment,
        AppointmentSlot slot,
        CancellationToken ct)
    {
        // Incrementing CurrentBookings on the tracked slot entity tells EF Core to
        // UPDATE it.  The IsRowVersion() configuration adds a WHERE xmin = <original>
        // clause.  If a concurrent transaction committed first this throws
        // DbUpdateConcurrencyException — caught by BookingService (AC-4).
        slot.CurrentBookings++;

        _context.Appointments.Add(appointment);

        // Single SaveChanges batches the slot UPDATE and appointment INSERT atomically.
        await _context.SaveChangesAsync(ct);

        return appointment;
    }

    // ── Cancel / Reschedule ───────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Appointment?> GetAppointmentAsync(
        Guid appointmentId, CancellationToken ct)
    {
        return await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
    }

    /// <inheritdoc />
    public async Task<Appointment?> GetAppointmentForPatientAsync(
        Guid appointmentId, Guid patientId, CancellationToken ct)
    {
        return await _context.Appointments
            .FirstOrDefaultAsync(
                a => a.Id == appointmentId && a.PatientId == patientId, ct);
    }

    /// <inheritdoc />
    public async Task<AppointmentSlot?> GetTrackedSlotAsync(
        Guid slotId, CancellationToken ct)
    {
        return await _context.AppointmentSlots
            .FirstOrDefaultAsync(s => s.Id == slotId, ct);
    }

    /// <inheritdoc />
    public async Task SaveAppointmentAsync(Appointment appointment, CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReleaseSlotAsync(Guid slotId, CancellationToken ct)
    {
        var slot = await _context.AppointmentSlots
            .FirstOrDefaultAsync(s => s.Id == slotId, ct);

        if (slot is not null && slot.CurrentBookings > 0)
        {
            slot.CurrentBookings--;
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<Appointment> RescheduleBookingAsync(
        Appointment appointment,
        AppointmentSlot oldSlot,
        AppointmentSlot newSlot,
        CancellationToken ct)
    {
        // Atomic: release old slot + reserve new slot + update appointment fields.
        // RowVersion checks on both slots fire in the single SaveChanges (AC-2).
        oldSlot.CurrentBookings--;
        newSlot.CurrentBookings++;

        appointment.SlotId          = newSlot.Id;
        appointment.ScheduledAt     = newSlot.StartTime;
        appointment.DurationMinutes = (int)newSlot.Duration;
        appointment.ProviderName    = newSlot.ProviderName;
        appointment.Location        = newSlot.Location;
        appointment.StaffUserId     = newSlot.ProviderId;

        await _context.SaveChangesAsync(ct);

        return appointment;
    }

    /// <inheritdoc />
    public async Task CreateAuditEntryAsync(
        AppointmentAuditEntry entry, CancellationToken ct)
    {
        _context.AppointmentAuditEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolvePatientIdAsync(Guid userId, CancellationToken ct)
    {
        var id = await _context.Patients
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        return id == Guid.Empty ? null : id;
    }
}
