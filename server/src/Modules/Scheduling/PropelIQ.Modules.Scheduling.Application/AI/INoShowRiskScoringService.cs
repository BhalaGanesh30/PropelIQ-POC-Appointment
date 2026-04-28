using PropelIQ.Modules.Scheduling.Application.AI.Models;

namespace PropelIQ.Modules.Scheduling.Application.AI;

/// <summary>
/// Evaluates the no-show risk for a confirmed appointment through the AI gateway.
/// AC-1: Returns a risk label (Low/Medium/High/Unknown) with explainable features.
/// AC-4: Result is cached against the appointment record with a RiskScoredAt timestamp.
/// Edge case 1: Returns Unknown when the AI gateway is unavailable.
/// Edge case 2: Recalculates when the cached score is older than 24 hours.
/// </summary>
public interface INoShowRiskScoringService
{
    /// <summary>
    /// Scores the no-show risk for the given appointment.
    /// Returns a cached score if one exists and is not stale (24-hour TTL).
    /// Falls back to <see cref="NoShowRiskDefaults.Unknown"/> on gateway failure.
    /// </summary>
    Task<NoShowRiskResult> ScoreAsync(
        Guid appointmentId,
        CancellationToken ct = default);
}
