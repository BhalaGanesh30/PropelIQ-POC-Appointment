namespace PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;

/// <summary>
/// Configuration for RFC 5545-compliant ICS calendar file generation.
/// Bound from the "Ics" section of appsettings.json.
/// </summary>
public sealed class IcsOptions
{
    /// <summary>appsettings.json section name.</summary>
    public const string SectionName = "Ics";

    /// <summary>
    /// RFC 5545 §3.7.3 PRODID — uniquely identifies the product that created the ICS.
    /// Format: -//Company//Product//Language
    /// </summary>
    public string ProductId { get; set; } = "-//PropelIQ//Appointment Scheduler//EN";

    /// <summary>
    /// IANA timezone identifier appended to DTSTART/DTEND as TZID.
    /// Explicit TZID prevents timezone conversion errors across calendar clients (edge case).
    /// Example: "America/New_York", "Europe/London", "UTC"
    /// </summary>
    public string DefaultTimezone { get; set; } = "UTC";

    /// <summary>
    /// Organizer mailto URI included in METHOD:REQUEST events per RFC 5545 §3.8.4.3.
    /// </summary>
    public string OrganizerEmail { get; set; } = "noreply@propeliq.com";
}
