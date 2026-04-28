using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Application.Reminders.Models;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// Reads and writes patient contact channel preferences from <see cref="AppDbContext"/>
/// to determine which notification channels (Email, Sms) reminders should target.
/// </summary>
public sealed class PatientPreferenceRepository : IPatientPreferenceRepository
{
    private readonly AppDbContext _db;

    public PatientPreferenceRepository(AppDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<NotificationPreferenceResponse> GetPreferencesAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        var row = await _db.Patients
            .AsNoTracking()
            .Where(p => p.UserId == patientId)
            .Select(p => new { p.ContactPreferences })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                $"Patient with UserId {patientId} not found.");

        var prefs = row.ContactPreferences;

        return new NotificationPreferenceResponse(
            EmailEnabled: prefs.EmailEnabled,
            SmsEnabled: prefs.SmsEnabled,
            ReminderTimings: prefs.ReminderTimings,
            HasPhoneNumber: !string.IsNullOrWhiteSpace(prefs.PreferredPhone));
    }

    /// <inheritdoc/>
    public async Task SavePreferencesAsync(
        Guid patientId,
        NotificationPreferenceDto dto,
        CancellationToken ct = default)
    {
        // Load, update, save — safer than ExecuteUpdateAsync on a JSON-owned entity.
        var patient = await _db.Patients
            .Where(p => p.UserId == patientId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                $"Patient with UserId {patientId} not found.");

        // Edge case 2: same-day reminders already in Pending/Sending status are
        // not touched here — they were created against the previous preference state
        // and will dispatch as originally scheduled.
        patient.ContactPreferences.EmailEnabled = dto.EmailEnabled;
        patient.ContactPreferences.SmsEnabled   = dto.SmsEnabled;
        patient.ContactPreferences.ReminderTimings = [.. dto.ReminderTimings];

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetEnabledChannelsAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        var prefs = await _db.Patients
            .Where(p => p.UserId == patientId)
            .Select(p => p.ContactPreferences)
            .FirstOrDefaultAsync(ct);

        if (prefs is null)
        {
            // Default to Email only when patient preferences are not found.
            return ["Email"];
        }

        var channels = new List<string>(2);

        if (prefs.EmailEnabled)
            channels.Add("Email");

        if (prefs.SmsEnabled)
            channels.Add("Sms");

        // AC-4: return empty list when all channels disabled so the dispatch
        // worker records the ReminderEvent.SendStatus as "OptedOut".
        return channels;
    }
}

