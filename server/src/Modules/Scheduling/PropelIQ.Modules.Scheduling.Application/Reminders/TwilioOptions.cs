namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Configuration POCO for Twilio SMS provider integration.
/// Bound from <c>appsettings.json</c> section <c>"Twilio"</c>.
///
/// NFR-007: <see cref="AccountSid"/> and <see cref="AuthToken"/> must be
/// stored in secure configuration and never logged or exposed.
/// </summary>
public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";

    /// <summary>Twilio Account SID — never log this value.</summary>
    public string AccountSid { get; init; } = string.Empty;

    /// <summary>Twilio Auth Token — never log this value.</summary>
    public string AuthToken { get; init; } = string.Empty;

    /// <summary>Twilio phone number used as the From number for SMS.</summary>
    public string FromNumber { get; init; } = string.Empty;

    /// <summary>
    /// Maximum concurrent SMS sends — SemaphoreSlim gate for Twilio rate limiting.
    /// Default: 10.
    /// </summary>
    public int MaxConcurrentSends { get; init; } = 10;
}
