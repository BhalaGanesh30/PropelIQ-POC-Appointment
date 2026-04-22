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
        // Extract OTP code from OTP emails (strong tag pattern).
        var otpStart = htmlBody.IndexOf("<strong>", StringComparison.OrdinalIgnoreCase);
        var otpCode = otpStart >= 0
            ? htmlBody[(otpStart + 8)..htmlBody.IndexOf("</strong>", otpStart, StringComparison.OrdinalIgnoreCase)]
            : null;

        // Extract confirmation URL from registration emails (href pattern).
        var urlStart = htmlBody.IndexOf("href='", StringComparison.Ordinal);
        var confirmUrl = urlStart >= 0
            ? htmlBody[(urlStart + 6)..htmlBody.IndexOf("'", urlStart + 6, StringComparison.Ordinal)]
            : null;

        if (otpCode is not null && otpCode.Length <= 10)
        {
            _logger.LogWarning(
                "\n======================================================" +
                "\n[STUB EMAIL] OTP Code for {To}: {Otp}" +
                "\n(Configure Email:Smtp in appsettings to send real emails)" +
                "\n======================================================",
                to, otpCode);
        }
        else if (confirmUrl is not null)
        {
            _logger.LogWarning(
                "\n======================================================" +
                "\n[STUB EMAIL] Confirmation URL for {To}:" +
                "\n{Url}" +
                "\n(Configure Email:Smtp in appsettings to send real emails)" +
                "\n======================================================",
                to, confirmUrl);
        }
        else
        {
            _logger.LogInformation(
                "[STUB EMAIL] To={To} | Subject={Subject} | Body={Body}",
                to, subject, htmlBody);
        }

        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "\n[STUB SMS] To={Phone} | Message={Message}" +
            "\n(No SMS provider configured — OTP is also sent via email)",
            phoneNumber, message);
        return Task.CompletedTask;
    }
}
