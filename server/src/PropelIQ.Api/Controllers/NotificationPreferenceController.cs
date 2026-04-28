using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Api.Authorization.Policies;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Application.Reminders.Models;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Patient notification preference API (US_029).
///
/// GET  /api/v1/notification-preferences — retrieve current channel and timing preferences.
/// PUT  /api/v1/notification-preferences — save updated preferences.
///
/// AC-1: Authenticated patients can toggle Email/SMS channels independently.
/// AC-2: Saved preferences apply to future reminders only; same-day reminders
///       already queued use the previously stored preference (edge case 2).
/// AC-3: Both channels returned by GetEnabledChannelsAsync when both enabled.
/// AC-4: Empty channel list returned by GetEnabledChannelsAsync when all disabled;
///       ReminderDispatchWorker records the event as OptedOut.
///
/// Edge case 1: SMS cannot be enabled without a verified phone number on file —
///             a 400 Bad Request is returned with a descriptive error.
/// OWASP A01: PatientOnly policy ensures patients cannot read or modify other
///            patients' preferences. All queries scope to the JWT userId claim.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.PatientOnly)]
[Route("api/v1/notification-preferences")]
public sealed class NotificationPreferenceController : BaseApiController
{
    private readonly IPatientPreferenceRepository _repo;

    public NotificationPreferenceController(IPatientPreferenceRepository repo)
        => _repo = repo;

    /// <summary>
    /// Retrieves the current notification preferences for the authenticated patient (AC-1).
    /// </summary>
    /// <response code="200">Current preferences including HasPhoneNumber flag.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller is not in the Patient role.</response>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationPreferenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = TryGetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var prefs = await _repo.GetPreferencesAsync(userId.Value, ct);
        return Ok(prefs);
    }

    /// <summary>
    /// Saves updated notification preferences for the authenticated patient (AC-2).
    /// Timing values are validated by <c>NotificationPreferenceValidator</c> (auto-validation).
    /// Returns the updated preference state including HasPhoneNumber.
    /// </summary>
    /// <response code="200">Updated preferences persisted and returned.</response>
    /// <response code="400">Invalid timing values, or SMS enabled without a phone number on file.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller is not in the Patient role.</response>
    [HttpPut]
    [ProducesResponseType(typeof(NotificationPreferenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromBody] NotificationPreferenceDto dto,
        CancellationToken ct)
    {
        var userId = TryGetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        // Edge case 1: SMS requires a verified phone number on file.
        // Check current preferences before saving to avoid overwriting state.
        if (dto.SmsEnabled)
        {
            var current = await _repo.GetPreferencesAsync(userId.Value, ct);
            if (!current.HasPhoneNumber)
            {
                return BadRequest(new
                {
                    Field   = "SmsEnabled",
                    Message = "A verified mobile number is required to enable SMS. " +
                              "Please add your phone number first.",
                });
            }
        }

        // AC-2: persist — affects future reminders only.
        await _repo.SavePreferencesAsync(userId.Value, dto, ct);

        // Return the updated state so the frontend can confirm persisted values.
        var updated = await _repo.GetPreferencesAsync(userId.Value, ct);
        return Ok(updated);
    }
}
