namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Configuration POCO for HMAC-signed reminder action tokens.
/// Bound from <c>appsettings.json</c> section <c>"ReminderToken"</c>.
///
/// NFR-007 / OWASP: <see cref="HmacSecret"/> must be stored in secure
/// configuration (environment variable or secrets manager) and never logged.
/// </summary>
public sealed class ReminderTokenOptions
{
    public const string SectionName = "ReminderToken";

    /// <summary>
    /// Base-64 encoded HMAC-SHA256 secret key (minimum 32 bytes / 256 bits).
    /// Never log this value.
    /// </summary>
    public string HmacSecret { get; init; } = string.Empty;

    /// <summary>
    /// Base URL for confirmation/cancellation links.
    /// Example: <c>http://localhost:5015</c> (dev) or <c>https://app.propeliq.com</c> (prod).
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;
}
