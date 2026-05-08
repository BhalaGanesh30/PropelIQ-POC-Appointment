using PropelIQ.Modules.Scheduling.Application.Override.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Executes a privileged scheduling override — bypasses the violated constraint,
/// performs the requested action, and writes an immutable audit record atomically
/// within a single database transaction per DR-002 (EP-004 US_034 FR-SO-004).
/// </summary>
public interface ISchedulingOverrideService
{
    /// <summary>
    /// Validates that the stated constraint actually applies to the appointment,
    /// executes the override action, and persists an immutable AuditRecord.
    /// </summary>
    /// <param name="request">Validated override payload from the controller.</param>
    /// <param name="staffUserId">Identity of the authenticated staff member performing the override.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Override result including override record ID and audit record ID.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the specified appointment does not exist (→ 404).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stated constraint does not actually apply to the appointment,
    /// preventing fabricated override payloads (→ 400 "Constraint does not apply").
    /// </exception>
    Task<SchedulingOverrideResponse> ExecuteOverrideAsync(
        SchedulingOverrideRequest request,
        Guid staffUserId,
        CancellationToken ct = default);
}
