using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Notifications;

/// <summary>
/// SMTP connection settings bound from the "Email:Smtp" configuration section.
/// All fields are required when the real SMTP sender is active.
/// </summary>
public sealed class SmtpSettings
{
    public const string SectionName = "Email:Smtp";

    [Required] public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    [Required] public string Username { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
    [Required] public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "PropelIQ";
}
