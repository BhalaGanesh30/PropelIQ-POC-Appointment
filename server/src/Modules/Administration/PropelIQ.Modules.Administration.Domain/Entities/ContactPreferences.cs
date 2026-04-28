namespace PropelIQ.Modules.Administration.Domain.Entities;

public sealed class ContactPreferences
{
    public bool SmsEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public string PreferredLanguage { get; set; } = "en";
    public string? PreferredPhone { get; set; }

    /// <summary>
    /// Reminder offset keys the patient wants to receive.
    /// Valid values: "7d", "2d", "1d", "2h".
    /// Defaults to all four offsets when no preference has been saved.
    /// </summary>
    public List<string> ReminderTimings { get; set; } = ["7d", "2d", "1d", "2h"];
}
