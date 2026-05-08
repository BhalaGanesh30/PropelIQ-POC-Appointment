using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.SharedServices.Application.Audit;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Drug-drug and drug-allergy conflict detection API (SCR-016 / FR-CA-003).
///
/// Routes:
///   GET  /api/v1/patients/{id}/conflicts  — Detect and return sorted conflict alerts.
///   POST /api/v1/conflicts/{id}/acknowledge — Clinician acknowledges a conflict alert.
///
/// Access matrix (SCR-016):
///   Clinician — Read + Acknowledge.
///   Staff     — Read only (POST returns HTTP 403).
///   Anonymous — HTTP 401 on both endpoints.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class ConflictController : BaseApiController
{
    private readonly IConflictDetectionService _detectionService;
    private readonly IConflictAlertRepository  _alertRepository;
    private readonly IConflictCacheService     _cache;
    private readonly IAuditService             _auditService;

    public ConflictController(
        IConflictDetectionService detectionService,
        IConflictAlertRepository alertRepository,
        IConflictCacheService cache,
        IAuditService auditService)
    {
        _detectionService = detectionService;
        _alertRepository  = alertRepository;
        _cache            = cache;
        _auditService     = auditService;
    }

    /// <summary>
    /// Detects and returns drug-drug and drug-allergy conflicts for the given patient.
    ///
    /// Results are cached in Redis for 30 seconds (TR-004).
    /// Sorted Critical → High → Moderate → Low (AC-1, AC-2).
    /// RulesStale flag set when conflict_rules are older than the staleness threshold (Edge Case 1).
    /// </summary>
    /// <param name="id">Patient GUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// HTTP 200 with <see cref="ConflictAlertsResponseDto"/> — always returned even when
    /// the patient has no conflicts (empty Alerts list).
    /// </returns>
    /// <response code="200">Conflict alerts returned (may be empty).</response>
    /// <response code="400">Invalid patient ID.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller lacks Clinician or Staff role.</response>
    [HttpGet("api/v1/patients/{id:guid}/conflicts")]
    [Authorize(Roles = "Clinician,Staff")]
    [ProducesResponseType(typeof(ConflictAlertsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetConflictsAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var response = await _detectionService.EvaluateConflictsAsync(id, ct);
        return Ok(response);
    }
    [HttpPost("api/v1/conflicts/{id:guid}/acknowledge")]
    [Authorize(Roles = "Clinician")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcknowledgeAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var clinicianId = TryGetCurrentUserId();
        if (clinicianId is null)
        {
            return Unauthorized();
        }

        // Load the alert to get patientId (needed for cache invalidation) and severity (for audit).
        var alert = await _alertRepository.GetByIdAsync(id, ct);
        if (alert is null)
        {
            return NotFound(new { error = $"Conflict alert {id} not found." });
        }

        var acknowledgedAt = DateTimeOffset.UtcNow;

        await _alertRepository.AcknowledgeAsync(id, clinicianId.Value, acknowledgedAt, ct);

        // Audit the acknowledgment (AC-4, NFR-010).
        await _auditService.LogEventAsync(
            eventType:        "conflict_acknowledged",
            actorUserId:      clinicianId.Value,
            targetEntityId:   id,
            targetEntityType: "conflict_alert",
            metadata: new Dictionary<string, string>
            {
                ["conflictId"]  = id.ToString(),
                ["clinicianId"] = clinicianId.Value.ToString(),
                ["severity"]    = alert.Severity,
                ["timestamp"]   = acknowledgedAt.ToString("O"),
            },
            ct: ct);

        // Invalidate the patient's conflict cache so the next GET reflects acknowledged = true.
        await _cache.InvalidateAsync(alert.PatientId, ct);

        return NoContent();
    }
}
