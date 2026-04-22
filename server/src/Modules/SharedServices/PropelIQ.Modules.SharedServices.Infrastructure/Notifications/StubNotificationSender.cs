using Microsoft.Extensions.Logging;
using PropelIQ.SharedKernel.Notifications;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Notifications;

/// <summary>
/// Development stub for <see cref="INotificationSender"/>.
/// Logs outbound notifications instead of calling real email/SMS providers.
/// Replace with production implementations (SendGrid, Twilio, etc.) via DI.
/// </summary>
internal sealed class StubNotificationSender : INotificationSender
{
    private readonly ILogger<StubNotificationSender> _logger;

    public StubNotificationSender(ILogger<StubNotificationSender> logger) =>
        _logger = logger;

    public Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        // Extract the href URL from the HTML body so the developer can copy it
        // directly from the console without needing a real email provider.
        var urlStart = htmlBody.IndexOf("href='", StringComparison.Ordinal);
        var confirmUrl = urlStart >= 0
            ? htmlBody[(urlStart + 6)..htmlBody.IndexOf("'", urlStart + 6, StringComparison.Ordinal)]
            : "(no URL found in body)";

        _logger.LogInformation(
            "[STUB EMAIL] To={To} | Subject={Subject}\n>>> Confirmation URL (copy into browser):\n{Url}",
            to, subject, confirmUrl);

        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB SMS] To={Phone} | Message={Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
