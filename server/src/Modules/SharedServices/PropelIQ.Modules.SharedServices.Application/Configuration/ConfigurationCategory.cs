namespace PropelIQ.Modules.SharedServices.Application.Configuration;

/// <summary>
/// Identifies the four manageable system configuration domains defined by FR-AD-001 (US_059).
/// </summary>
public enum ConfigurationCategory
{
    /// <summary>Appointment slot templates: duration, buffer time, availability windows.</summary>
    SlotTemplates,

    /// <summary>Patient reminder rules: cadence intervals, channel preferences, escalation thresholds.</summary>
    ReminderRules,

    /// <summary>Session policy: timeout minutes, warning lead time, max concurrent sessions.</summary>
    SessionPolicy,

    /// <summary>Communication templates: default sender, reply-to address, footer text.</summary>
    CommunicationTemplates
}
