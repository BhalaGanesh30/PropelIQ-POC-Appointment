using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.SharedServices.Application.Kpi;

namespace PropelIQ.Api.Controllers.Admin;

/// <summary>
/// Admin-only KPI dashboard REST API (US_060, AC-1–AC-4, edge cases 1–2).
///
/// <list type="bullet">
///   <item><c>GET  /api/v1/admin/kpi/summary</c>               — four KPI cards for a date range (AC-1, AC-2).</item>
///   <item><c>GET  /api/v1/admin/kpi/timeseries/{metric}</c>   — daily data points for a single metric (AC-2).</item>
///   <item><c>POST /api/v1/admin/kpi/export</c>                — PDF or PNG export within 3 s (AC-3).</item>
/// </list>
///
/// <para>All endpoints require the <c>Admin</c> role (US_015 authorization infrastructure).</para>
/// <para>
/// Date range validation is performed manually via <see cref="IValidator{T}"/> so that
/// query-string parameters are validated before any database work.
/// </para>
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/kpi")]
[ApiController]
[Produces("application/json")]
public sealed class KpiDashboardController : BaseApiController
{
    private readonly IKpiMetricsService      _metrics;
    private readonly IValidator<DateRange>   _rangeValidator;

    public KpiDashboardController(
        IKpiMetricsService      metrics,
        IValidator<DateRange>   rangeValidator)
    {
        _metrics        = metrics;
        _rangeValidator = rangeValidator;
    }

    /// <summary>
    /// Returns four KPI card values for the given date range (AC-1, AC-2).
    ///
    /// <para>
    /// <see cref="KpiSummaryResponse.IsStale"/> is <c>true</c> when the cached result is older
    /// than 1 hour — the UI should display a staleness warning (edge case 1).
    /// </para>
    ///
    /// <para>
    /// All card values are 0 when no appointments exist in the range (edge case 2).
    /// </para>
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(KpiSummaryResponse), 200)]
    [ProducesResponseType(typeof(IEnumerable<string>), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var range      = new DateRange(from, to);
        var validation = await _rangeValidator.ValidateAsync(range, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var result = await _metrics.GetSummaryAsync(range, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns daily data points for a single KPI metric over the given date range (AC-2).
    /// Points are empty when no daily metrics data exists for the period (edge case 2).
    /// </summary>
    [HttpGet("timeseries/{metric}")]
    [ProducesResponseType(typeof(KpiTimeSeriesResponse), 200)]
    [ProducesResponseType(typeof(IEnumerable<string>), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetTimeSeries(
        KpiMetricType metric,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var range      = new DateRange(from, to);
        var validation = await _rangeValidator.ValidateAsync(range, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var result = await _metrics.GetTimeSeriesAsync(metric, range, ct);
        return Ok(result);
    }

    /// <summary>
    /// Exports the KPI summary for the given date range as a PDF or PNG file (AC-3).
    ///
    /// <para>AC-3 SLA: export must complete within 3 seconds for ranges ≤ 365 days.</para>
    /// </summary>
    [HttpPost("export")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(IEnumerable<string>), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Export(
        [FromBody] KpiExportRequest request,
        CancellationToken ct)
    {
        var validation = await _rangeValidator.ValidateAsync(request.Range, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var result = await _metrics.ExportAsync(request, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
