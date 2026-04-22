namespace PropelIQ.SharedKernel.Notifications;

/// <summary>
/// Abstraction for sending email and SMS notifications.
/// Production implementations will be registered per environment;
/// StubNotificationSender handles development/test environments.
/// </summary>
public interface INotificationSender
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default);
}
