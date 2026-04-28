using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.AI;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.AI;

/// <summary>
/// EF Core implementation of <see cref="IPatientHistoryFeatureExtractor"/>.
/// Queries the appointments and reminder events tables to build aggregated,
/// PII-free features for the no-show risk scoring prompt (AIR-009).
/// </summary>
public sealed class PatientHistoryFeatureExtractor : IPatientHistoryFeatureExtractor
{
    private readonly AppDbContext _db;

    public PatientHistoryFeatureExtractor(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<PatientHistoryFeatures> ExtractAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        // Load aggregated appointment history — no PII columns selected (AIR-009).
        var appointments = await _db.Appointments
            .AsNoTracking()
            .Where(a => a.PatientId == patientId)
            .Select(a => new
            {
                a.Status,
                a.ScheduledAt,
                a.CreatedAt
            })
            .ToListAsync(ct);

        var total = appointments.Count;
        var noShows = appointments.Count(a => a.Status == "No-Show");
        var cancellations = appointments.Count(a => a.Status == "Cancelled");

        // Count appointments where the patient responded "Confirmed" to a reminder.
        var confirmedViaReminder = await _db.ReminderEvents
            .AsNoTracking()
            .Where(r =>
                r.ConfirmationResponse == "Confirmed"
                && _db.Appointments
                    .Where(a => a.PatientId == patientId)
                    .Select(a => a.Id)
                    .Contains(r.AppointmentId))
            .CountAsync(ct);

        // Average lead time in days from when the booking was made to the scheduled time.
        var avgLeadDays = total > 0
            ? Math.Round(
                appointments.Average(a =>
                    (a.ScheduledAt - a.CreatedAt).TotalDays),
                1)
            : 0.0;

        // Use current wall-clock for day-of-week and time-of-day features so the
        // prompt reflects what time-slot the patient is booking into now.
        var now = DateTime.UtcNow;

        return new PatientHistoryFeatures(
            TotalAppointments: total,
            NoShowCount: noShows,
            CancellationCount: cancellations,
            ConfirmedViaReminderCount: confirmedViaReminder,
            AverageLeadTimeDays: avgLeadDays,
            DayOfWeek: now.DayOfWeek.ToString(),
            TimeOfDay: now.Hour switch
            {
                < 12 => "Morning",
                < 17 => "Afternoon",
                _    => "Evening"
            });
    }
}
