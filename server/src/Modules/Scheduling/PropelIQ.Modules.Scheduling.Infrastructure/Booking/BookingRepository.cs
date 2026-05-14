using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.AI.Models;
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

        if (id != Guid.Empty) return id;

        // No patient yet — auto-provision a minimal record from the domain user projection.
        var domainUser = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync(ct);

        if (domainUser is null) return null;

        var mrn = $"AUTO-{userId.ToString("N")[..8].ToUpperInvariant()}";

        // Guard against a race condition where two concurrent requests both reach here.
        var mrnExists = await _context.Patients.AnyAsync(p => p.MRN == mrn, ct);
        if (mrnExists)
        {
            return await _context.Patients
                .Where(p => p.UserId == userId)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        var patient = new PropelIQ.Modules.Administration.Domain.Entities.Patient
        {
            UserId      = userId,
            FirstName   = domainUser.FirstName ?? "Unknown",
            LastName    = domainUser.LastName  ?? "Unknown",
            DateOfBirth = new DateOnly(1900, 1, 1), // Placeholder — updated when intake form is submitted.
            MRN         = mrn,
        };

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync(ct);
        return patient.Id;
    }

    // ── No-show risk score (US_028) ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task UpdateRiskScoreAsync(
        Guid appointmentId,
        string riskLevel,
        double confidence,
        string featuresJson,
        DateTimeOffset scoredAt,
        CancellationToken ct = default)
    {
        await _context.Appointments
            .Where(a => a.Id == appointmentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.RiskLevel, riskLevel)
                .SetProperty(a => a.RiskConfidence, confidence)
                .SetProperty(a => a.RiskFeatures, featuresJson)
                .SetProperty(a => a.RiskScoredAt, scoredAt),
                ct);
    }

    // ── Risk dashboard queries (US_028 task_002) ──────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppointmentRiskProjection>>
        GetUpcomingForRiskDashboardAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken ct = default)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(a =>
                a.ScheduledAt >= from
                && a.ScheduledAt <= to
                && a.Status != "Cancelled")
            .OrderBy(a => a.ScheduledAt)
            .Join(
                _context.Patients,
                a => a.PatientId,
                p => p.Id,
                (a, p) => new AppointmentRiskProjection(
                    a.Id,
                    p.FirstName + " " + p.LastName,
                    a.ScheduledAt,
                    a.AppointmentType,
                    a.Status,
                    a.RiskLevel,
                    a.RiskConfidence))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task ClearRiskScoreAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        await _context.Appointments
            .Where(a => a.Id == appointmentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.RiskLevel, (string?)null)
                .SetProperty(a => a.RiskConfidence, (double?)null)
                .SetProperty(a => a.RiskFeatures, (string?)null)
                .SetProperty(a => a.RiskScoredAt, (DateTimeOffset?)null),
                ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetAppointmentsNeedingRiskScoreAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset staleThreshold,
        int limit,
        CancellationToken ct = default)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(a =>
                a.ScheduledAt >= from
                && a.ScheduledAt <= to
                && a.Status == AppointmentStatus.Confirmed.ToString()
                && (a.RiskScoredAt == null || a.RiskScoredAt < staleThreshold))
            .OrderBy(a => a.ScheduledAt)
            .Take(limit)
            .Select(a => a.Id)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppointmentRiskProjection>>
        GetHighRiskAppointmentsInWindowAsync(
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(a =>
                a.RiskLevel == "High"
                && a.ScheduledAt >= windowStart
                && a.ScheduledAt <= windowEnd
                && a.Status == AppointmentStatus.Confirmed.ToString())
            .Join(
                _context.Patients,
                a => a.PatientId,
                p => p.Id,
                (a, p) => new AppointmentRiskProjection(
                    a.Id,
                    p.FirstName + " " + p.LastName,
                    a.ScheduledAt,
                    a.AppointmentType,
                    a.Status,
                    a.RiskLevel,
                    a.RiskConfidence))
            .ToListAsync(ct);
    }
}
