namespace PropelIQ.Modules.Scheduling.Application.Schedule.Dto;

/// <summary>
/// Per-appointment entry returned by <c>GET /api/v1/schedule/daily</c> (AC-1).
/// Provides all fields required for the time-grid calendar block and print layout (AC-3).
/// </summary>
public sealed class DailyScheduleEntryDto
{
    public required Guid AppointmentId { get; init; }
    /// <summary>Patient display name ("First Last") — used as block label and in print table.</summary>
    public required string PatientName { get; init; }
    /// <summary>Appointment type key — maps to block colour in the FE time-grid.</summary>
    public required string AppointmentType { get; init; }
    /// <summary>UTC start time of the appointment (ISO-8601).</summary>
    public required DateTimeOffset StartTime { get; init; }
    public required int DurationMinutes { get; init; }
    /// <summary>Current lifecycle status (e.g. "Confirmed", "CheckedIn").</summary>
    public required string Status { get; init; }
    /// <summary>Optional provider or clinician name shown inside the block.</summary>
    public string? ProviderName { get; init; }
    /// <summary>Optional room or location shown inside the block.</summary>
    public string? Location { get; init; }
}
