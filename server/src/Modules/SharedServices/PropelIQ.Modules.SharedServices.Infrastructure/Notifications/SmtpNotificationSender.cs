using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.SharedKernel.Notifications;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Notifications;

/// <summary>
/// Real SMTP implementation of <see cref="INotificationSender"/>.
/// Activated when the "Email:Smtp:Host" configuration value is present.
/// Uses System.Net.Mail.SmtpClient — no extra packages required.
/// </summary>
internal sealed class SmtpNotificationSender : INotificationSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpNotificationSender> _logger;

    public SmtpNotificationSender(
        IOptions<SmtpSettings> settings,
        ILogger<SmtpNotificationSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        try
        {
            await client.SendMailAsync(message, ct);
            _logger.LogInformation("[SMTP] Email sent to {To} | Subject: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMTP] Failed to send email to {To} | Subject: {Subject}", to, subject);
            throw;
        }
    }

    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        _logger.LogWarning("[SMTP Sender] SMS not implemented. Phone={Phone}", phoneNumber);
        return Task.CompletedTask;
    }
}
