namespace PropelIQ.Modules.Scheduling.Infrastructure.Queue;

/// <summary>
/// Configuration options for wait-time estimation (EP-004 US_031 task_003).
/// Bound from <c>appsettings.json</c> section <c>"WaitTime"</c>.
/// </summary>
public sealed class WaitTimeOptions
{
    public const string SectionName = "WaitTime";

    /// <summary>
    /// Fallback service duration in minutes used when an appointment type is not
    /// present in <see cref="AppointmentTypeDurations"/>.
    /// Default: 15 minutes.
    /// </summary>
    public int DefaultServiceDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Per-type service duration overrides.
    /// Key: appointment type code (case-sensitive, matches <c>Appointment.AppointmentType</c>).
    /// Value: estimated service duration in minutes for that type.
    /// Example: <c>{ "GENERAL": 20, "FOLLOWUP": 10, "URGENT": 30 }</c>
    /// </summary>
    public Dictionary<string, int> AppointmentTypeDurations { get; set; } = new();
}
