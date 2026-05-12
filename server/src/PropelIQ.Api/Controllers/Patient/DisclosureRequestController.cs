using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Api.Authorization.Policies;
using PropelIQ.Modules.SharedServices.Application.Disclosure;

namespace PropelIQ.Api.Controllers.Patient;

/// <summary>
/// Patient-facing endpoints for submitting and tracking personal data disclosure
/// requests (US_057, AC-2, edge case 1).
///
/// All routes are under <c>/api/v1/patients/me/disclosure-requests</c> and require
/// the <c>PatientOnly</c> authorization policy so that only the requesting patient
/// can access their own disclosure records.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.PatientOnly)]
[Route("api/v1/patients/me/disclosure-requests")]
[ApiController]
[Produces("application/json")]
public sealed class DisclosureRequestController : BaseApiController
{
    private readonly IDisclosureService _service;

    public DisclosureRequestController(IDisclosureService service)
    {
        _service = service;
    }

    /// <summary>
    /// Submits a new disclosure request for the authenticated patient (AC-2).
    ///
    /// Returns 201 Created with the new request ID and initial status "Submitted".
    /// The <see cref="DisclosureCompilationWorker"/> will asynchronously compile
    /// matching access logs and transition the request to PendingReview.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitDisclosureRequest request,
        CancellationToken ct)
    {
        var patientId = TryGetCurrentUserId();
        if (patientId is null) return Unauthorized();

        var id = await _service.SubmitAsync(patientId.Value, request.FromDateUtc, request.ToDateUtc, ct);

        return CreatedAtAction(nameof(GetStatus), new { id }, new { id, status = "Submitted" });
    }

    /// <summary>
    /// Returns the status and metadata for a specific disclosure request belonging to the patient.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DisclosureRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var patientId = TryGetCurrentUserId();
        if (patientId is null) return Unauthorized();

        var result = await _service.GetByIdForPatientAsync(patientId.Value, id, ct);
        if (result is null) return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Lists all disclosure requests for the authenticated patient (most-recent first).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DisclosureRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var patientId = TryGetCurrentUserId();
        if (patientId is null) return Unauthorized();

        var results = await _service.ListForPatientAsync(patientId.Value, ct);
        return Ok(results);
    }

    /// <summary>
    /// Downloads the compiled disclosure report using a 48-hour HMAC-signed token
    /// delivered to the patient via email on approval (AC-3, edge case 1).
    ///
    /// Returns 410 Gone when the download token has expired.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Download(
        Guid id,
        [FromQuery] string token,
        CancellationToken ct)
    {
        var patientId = TryGetCurrentUserId();
        if (patientId is null) return Unauthorized();

        var result = await _service.GetReportForDownloadAsync(patientId.Value, id, token, ct);
        if (result is null) return NotFound();

        if (result.IsExpired)
            return StatusCode(StatusCodes.Status410Gone, new { message = "Download link has expired. Please request a new disclosure." });

        return File(result.Content, "application/json", $"disclosure-report-{id}.json");
    }
}
