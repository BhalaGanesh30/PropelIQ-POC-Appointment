namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Configuration POCO for SendGrid email provider integration.
/// Bound from <c>appsettings.json</c> section <c>"SendGrid"</c>.
///
/// NFR-007: <see cref="ApiKey"/> must be stored in secure configuration
/// (environment variable or secrets manager) and never logged or exposed.
/// </summary>
public sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";

    /// <summary>SendGrid API key — never log this value.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Sender email address shown in the From field.</summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>Sender display name shown alongside the From address.</summary>
    public string FromName { get; init; } = string.Empty;

    /// <summary>Optional SendGrid dynamic template ID for reminder emails.</summary>
    public string? ReminderTemplateId { get; init; }
}
