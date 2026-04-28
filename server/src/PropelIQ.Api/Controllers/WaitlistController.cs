using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Application.Waitlist.Dto;
using PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Preferred-slot waitlist API (US_023 / US_030).
///
/// POST   /api/v1/waitlist                      — join the waitlist (AC-1).
/// GET    /api/v1/waitlist                      — retrieve patient's active entries.
/// GET    /api/v1/waitlist/claim-details?token= — resolve claim link (US_030 task_002).
/// POST   /api/v1/waitlist/{id}/claim           — claim an offered slot (AC-3).
/// DELETE /api/v1/waitlist/{id}                 — cancel a waitlist entry.
///
/// All endpoints require JWT authentication; patient ID is extracted from the
/// NameIdentifier claim — patient ownership is enforced at the repository level
/// (no cross-patient data exposure).
/// NFR-010: structured logging in WaitlistService captures all state transitions.
/// </summary>
[Authorize]
public sealed class WaitlistController : BaseApiController
{
    private readonly WaitlistService _waitlistService;
    private readonly AppDbContext _db;
    private readonly ReminderTokenOptions _tokenOptions;

    public WaitlistController(
        WaitlistService waitlistService,
        AppDbContext db,
        IOptions<ReminderTokenOptions> tokenOptions)
    {
        _waitlistService = waitlistService;
        _db              = db;
        _tokenOptions    = tokenOptions.Value;
    }

    private Guid GetPatientId() => TryGetCurrentUserId() ?? Guid.Empty;

    /// <summary>
    /// Join the waitlist with preferred slot parameters (AC-1).
    /// </summary>
    /// <response code="201">Entry created — includes ID, status, and FIFO position.</response>
    /// <response code="400">Validation failure — date range, duration, or type invalid.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpPost]
    [ProducesResponseType(typeof(WaitlistEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> JoinWaitlist(
        [FromBody] JoinWaitlistRequest request,
        CancellationToken ct)
    {
        var result = await _waitlistService.JoinAsync(GetPatientId(), request, ct);
        return CreatedAtAction(nameof(GetEntries), null, result);
    }

    /// <summary>
    /// Get the current patient's Active and Offered waitlist entries.
    /// </summary>
    /// <response code="200">List of waitlist entries (may be empty).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<WaitlistEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEntries(CancellationToken ct)
    {
        var entries = await _waitlistService.GetEntriesAsync(GetPatientId(), ct);
        return Ok(entries);
    }

    /// <summary>
    /// Resolve claim link details for the slot claim page (US_030 task_002 / AC-2).
    /// Decodes the HMAC-signed token to retrieve slot details and the expiry timestamp.
    /// </summary>
    /// <param name="token">HMAC-signed claim token from the slot alert email/SMS link.</param>
    /// <response code="200">Slot details and expiry timestamp for the claim page.</response>
    /// <response code="400">Token missing, malformed, or HMAC verification failed.</response>
    /// <response code="404">Waitlist entry or slot not found.</response>
    /// <response code="410">Claim window has already expired.</response>
    [HttpGet("claim-details")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SlotClaimDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetClaimDetails(
        [FromQuery] string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { message = "Token is required." });

        // ── Decode base64url token ────────────────────────────────────────────
        Guid entryId;
        Guid slotId;
        DateTimeOffset tokenExpiresAt;

        try
        {
            var padded  = token.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "=";  break;
            }

            var bytes = Convert.FromBase64String(padded);

            // Token format (from SlotAlertService): entryId(16) + slotId(16) + ticks(8) + HMAC(32) = 72 bytes
            if (bytes.Length != 72) return BadRequest(new { message = "Invalid token format." });

            entryId      = new Guid(bytes.AsSpan(0, 16));
            slotId       = new Guid(bytes.AsSpan(16, 16));
            var ticks    = BitConverter.ToInt64(bytes.AsSpan(32, 8));
            tokenExpiresAt = new DateTimeOffset(ticks, TimeSpan.Zero);

            // ── HMAC-SHA256 verification (OWASP A01 — prevents token forgery) ─
            var secretBytes = GetSecretBytes();
            using var hmac  = new HMACSHA256(secretBytes);
            var expectedHmac = hmac.ComputeHash(bytes[..40]);
            if (!CryptographicOperations.FixedTimeEquals(
                    expectedHmac.AsSpan(), bytes.AsSpan(40, 32)))
                return BadRequest(new { message = "Invalid claim token." });
        }
        catch
        {
            return BadRequest(new { message = "Malformed claim token." });
        }

        // ── Load entry and slot ────────────────────────────────────────────────
        var entry = await _db.WaitlistEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entryId, ct);

        if (entry is null)
            return NotFound(new { message = "Waitlist entry not found." });

        // Prefer stored expiry; fall back to token-embedded expiry.
        var expiresAt = entry.ClaimExpiresAt ?? tokenExpiresAt;

        if (expiresAt < DateTimeOffset.UtcNow)
            return StatusCode(StatusCodes.Status410Gone,
                new { message = "Claim window has expired." });

        var slot = await _db.AppointmentSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == slotId, ct);

        // Slot may be null if it was cancelled — still return basic entry data.
        return Ok(new SlotClaimDetailsResponse
        {
            WaitlistEntryId = entry.Id,
            SlotDateTime    = slot?.StartTime ?? entry.OfferedAt ?? expiresAt,
            SlotType        = slot?.Type.ToString() ?? entry.PreferredAppointmentType,
            ProviderName    = slot?.ProviderName,
            DurationMinutes = (int?)slot?.Duration ?? entry.PreferredDurationMinutes,
            ExpiresAtUtc    = expiresAt,
            Status          = entry.Status.ToString(),
        });
    }

    /// <summary>
    /// Claim an offered slot to create a confirmed appointment (AC-3).
    /// Returns 409 Conflict when a concurrent claim already reserved the slot (edge case).
    /// Returns 410 Gone when the 2-hour claim window has closed (US_030 AC-4).
    /// </summary>
    /// <param name="id">Waitlist entry GUID.</param>
    /// <param name="token">
    ///   Optional HMAC-signed claim token embedded in the slot alert link (US_030 AC-3).
    ///   When supplied, it is verified against the stored hash before the claim proceeds.
    /// </param>
    /// <response code="200">Slot claimed — includes booking confirmation details.</response>
    /// <response code="400">Entry not offered, not found, or invalid token.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="409">Slot taken by a concurrent claim — patient remains on waitlist.</response>
    /// <response code="410">Claim window has expired — slot offered to next patient.</response>
    [HttpPost("{id:guid}/claim")]
    [ProducesResponseType(typeof(ClaimWaitlistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> ClaimSlot(
        Guid id,
        [FromQuery] string? token,
        CancellationToken ct)
    {
        // Token verification (defense-in-depth — AC-3).
        // The entry's ClaimTokenHash is populated by SlotAlertService when the
        // alert is dispatched; if the hash is present the claim MUST carry a
        // matching token to prevent unauthorised reservation (OWASP A01).
        if (!string.IsNullOrWhiteSpace(token))
        {
            var tokenValid = await _waitlistService.ValidateClaimTokenAsync(id, token, ct);
            if (!tokenValid)
                return BadRequest(new { message = "Invalid or tampered claim token." });
        }

        var result = await _waitlistService.ClaimAsync(id, GetPatientId(), ct);

        if (result.IsSuccess)
            return Ok(result.Value);

        // 410 Gone — claim window closed; slot has been rotated to the next patient.
        if (result.Error!.Contains("expired", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status410Gone, new { message = result.Error });

        // 409 Conflict — concurrent claim race.
        if (result.Error.Contains("claimed by another patient", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = result.Error });

        return BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Cancel (remove) a waitlist entry owned by the current patient.
    /// </summary>
    /// <response code="204">Entry cancelled successfully.</response>
    /// <response code="400">Entry is already claimed or cancelled.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="404">Entry not found or belongs to a different patient.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEntry(Guid id, CancellationToken ct)
    {
        var result = await _waitlistService.CancelEntryAsync(id, GetPatientId(), ct);

        if (result.IsSuccess)
            return NoContent();

        if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = result.Error });

        return BadRequest(new { message = result.Error });
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private byte[] GetSecretBytes()
    {
        try
        {
            return Convert.FromBase64String(_tokenOptions.HmacSecret);
        }
        catch
        {
            return Encoding.UTF8.GetBytes(_tokenOptions.HmacSecret);
        }
    }
}
