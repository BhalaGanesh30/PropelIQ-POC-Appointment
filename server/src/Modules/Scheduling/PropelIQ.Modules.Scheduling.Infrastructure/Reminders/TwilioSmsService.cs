using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// AC-2: Sends concise reminder SMS via Twilio containing appointment time
/// and a short-link for confirmation or cancellation.
///
/// Edge case: Twilio rate limiting is mitigated via a <see cref="SemaphoreSlim"/>
/// concurrency gate configured from <see cref="TwilioOptions.MaxConcurrentSends"/>.
/// <c>ApiException</c> with status 429 is caught and re-thrown as
/// <see cref="HttpRequestException"/> to signal the Polly retry pipeline.
///
/// NFR-007: <see cref="TwilioOptions.AccountSid"/> and <see cref="TwilioOptions.AuthToken"/>
/// are injected via <c>IOptions</c> and never logged or included in error messages.
/// </summary>
public sealed class TwilioSmsService : IReminderSmsService, IDisposable
{
    private readonly ITwilioRestClient _client;
    private readonly TwilioOptions _options;
    private readonly IReminderTokenService _tokenService;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(
        ITwilioRestClient client,
        IOptions<TwilioOptions> options,
        IReminderTokenService tokenService,
        ILogger<TwilioSmsService> logger)
    {
        _client         = client;
        _options        = options.Value;
        _tokenService   = tokenService;
        _logger         = logger;
        _concurrencyGate = new SemaphoreSlim(_options.MaxConcurrentSends);
    }

    /// <inheritdoc/>
    public async Task SendReminderSmsAsync(
        ReminderEvent reminder,
        string phoneNumber,
        Appointment appointment,
        CancellationToken ct = default)
    {
        await _concurrencyGate.WaitAsync(ct);
        try
        {
            var actionUrl = _tokenService.GenerateActionUrl(
                reminder.AppointmentId, reminder.Id);

            var body =
                $"Reminder: Appointment on {appointment.ScheduledAt:MMM dd} at " +
                $"{appointment.ScheduledAt:hh:mm tt}. " +
                $"Confirm or cancel: {actionUrl}";

            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(phoneNumber),
                from: new PhoneNumber(_options.FromNumber),
                body: body,
                client: _client);

            if (message.ErrorCode is not null)
            {
                _logger.LogWarning(
                    "Twilio error {ErrorCode} for reminder {ReminderId}: {ErrorMessage}.",
                    message.ErrorCode, reminder.Id, message.ErrorMessage);
                throw new HttpRequestException(
                    $"Twilio delivery failed with error code {message.ErrorCode}.");
            }

            _logger.LogInformation(
                "Reminder SMS sent via Twilio for appointment {AppointmentId} " +
                "to {PhoneNumber}.",
                reminder.AppointmentId, phoneNumber);
        }
        catch (ApiException ex) when (ex.Status == 429)
        {
            // Edge case: Twilio rate limiting — signal retry pipeline.
            _logger.LogWarning(
                "Twilio rate limited (429) for reminder {ReminderId}. " +
                "Will retry on next dispatch cycle.",
                reminder.Id);
            throw new HttpRequestException("Twilio rate limited (429).", ex);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    public void Dispose() => _concurrencyGate.Dispose();
}
