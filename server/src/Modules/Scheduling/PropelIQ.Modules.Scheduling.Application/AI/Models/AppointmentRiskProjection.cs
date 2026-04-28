namespace PropelIQ.Modules.Scheduling.Application.AI.Models;

/// <summary>
/// Lightweight read-only projection of an Appointment joined with the patient name.
/// Used by the risk dashboard API endpoint and the HighRiskNotificationWorker to
/// avoid loading full entity graphs when only a subset of columns is required.
/// </summary>
public sealed record AppointmentRiskProjection(
    Guid Id,
    string PatientName,
    DateTimeOffset ScheduledAt,
    string AppointmentType,
    string Status,
    string? RiskLevel,
    double? RiskConfidence);
