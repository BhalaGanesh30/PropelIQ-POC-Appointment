namespace PropelIQ.Modules.Scheduling.Application.AI.Models;

/// <summary>
/// Event published to the in-process Channel when a High-risk appointment is
/// detected within the 24-hour notification window (AC-3, US_028).
/// Consumed by notification infrastructure to alert the assigned staff member.
/// </summary>
public sealed record HighRiskAlertEvent(
    Guid AppointmentId,
    string PatientName,
    DateTimeOffset AppointmentDate,
    string RiskLevel,
    double Confidence);
