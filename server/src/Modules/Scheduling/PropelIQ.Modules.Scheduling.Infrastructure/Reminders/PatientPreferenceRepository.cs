using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// Reads patient contact channel preferences from <see cref="AppDbContext"/>
/// to determine which notification channels (Email, Sms) reminders should target.
/// </summary>
public sealed class PatientPreferenceRepository : IPatientPreferenceRepository
{
    private readonly AppDbContext _db;

    public PatientPreferenceRepository(AppDbContext db) => _db = db;

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

        // Ensure at least one channel is always returned.
        return channels.Count > 0 ? channels : ["Email"];
    }
}
