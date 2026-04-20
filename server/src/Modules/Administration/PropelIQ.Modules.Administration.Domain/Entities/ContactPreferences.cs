namespace PropelIQ.Modules.Administration.Domain.Entities;

public sealed class ContactPreferences
{
    public bool SmsEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public string PreferredLanguage { get; set; } = "en";
    public string? PreferredPhone { get; set; }
}
