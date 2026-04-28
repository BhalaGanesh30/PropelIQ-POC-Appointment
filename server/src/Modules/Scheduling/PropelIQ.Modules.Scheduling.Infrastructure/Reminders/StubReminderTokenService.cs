using PropelIQ.Modules.Scheduling.Application.Reminders;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// Development placeholder for <see cref="IReminderTokenService"/>.
/// Returns non-signed URLs pointing to the local API.
/// Replaced by <see cref="ReminderTokenService"/> when ReminderToken config is present.
/// </summary>
public sealed class StubReminderTokenService : IReminderTokenService
{
    private const string BaseUrl = "http://localhost:5015/api/v1/reminders";

    /// <inheritdoc/>
    public string GenerateConfirmUrl(Guid appointmentId, Guid reminderId) =>
        $"{BaseUrl}/confirm?appointmentId={appointmentId}&reminderId={reminderId}";

    /// <inheritdoc/>
    public string GenerateCancelUrl(Guid appointmentId, Guid reminderId) =>
        $"{BaseUrl}/cancel?appointmentId={appointmentId}&reminderId={reminderId}";

    /// <inheritdoc/>
    public string GenerateActionUrl(Guid appointmentId, Guid reminderId) =>
        $"{BaseUrl}/action?appointmentId={appointmentId}&reminderId={reminderId}";

    /// <inheritdoc/>
    /// <remarks>Stub always returns <c>null</c> — use real <see cref="ReminderTokenService"/> for validation.</remarks>
    public ReminderTokenPayload? ValidateToken(string token) => null;
}
