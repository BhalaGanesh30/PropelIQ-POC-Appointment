using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Api.Authorization.Policies;
using PropelIQ.Modules.SharedServices.Application.Disclosure;

namespace PropelIQ.Api.Controllers.Admin;

/// <summary>
/// Staff/Admin endpoints for reviewing and approving patient disclosure requests (US_057, AC-3).
///
/// Routes under <c>/api/v1/admin/disclosure-requests</c> require the
/// <c>StaffOrAdmin</c> authorization policy.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
[Route("api/v1/admin/disclosure-requests")]
[ApiController]
[Produces("application/json")]
public sealed class DisclosureReviewController : BaseApiController
{
    private readonly IDisclosureService _service;

    public DisclosureReviewController(IDisclosureService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lists disclosure requests for staff review. Supports optional status filter
    /// (e.g., "PendingReview") with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DisclosureRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPending(
        [FromQuery] string? status      = null,
        [FromQuery] int     page        = 1,
        [FromQuery] int     pageSize    = 25,
        CancellationToken   ct          = default)
    {
        var results = await _service.ListForReviewAsync(status, page, pageSize, ct);
        return Ok(results);
    }

    /// <summary>
    /// Reviews a disclosure request — approves or rejects it (AC-3).
    ///
    /// On approval the service generates a 48-hour HMAC download token,
    /// sends the patient a secure download email, and transitions the request
    /// to Delivered.
    /// </summary>
    [HttpPut("{id:guid}/review")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewDisclosureRequest request,
        CancellationToken ct)
    {
        var reviewerId = TryGetCurrentUserId();
        if (reviewerId is null) return Unauthorized();

        var success = await _service.ReviewAsync(id, reviewerId.Value, request.Approved, request.Notes, ct);
        if (!success) return NotFound();

        return Ok(new { status = request.Approved ? "Approved" : "Rejected" });
    }

    /// <summary>
    /// Returns the compiled report JSON for staff preview prior to approval.
    /// </summary>
    [HttpGet("{id:guid}/report")]
    [ProducesResponseType(typeof(DisclosureReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ViewReport(Guid id, CancellationToken ct)
    {
        var report = await _service.GetReportForReviewAsync(id, ct);
        if (report is null) return NotFound();

        return Ok(report);
    }
}
