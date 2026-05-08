using PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Business logic contract for walk-in creation and patient conversion
/// (EP-004 US_033).
///
/// Implemented by <c>WalkinService</c> in the Infrastructure layer.
/// </summary>
public interface IWalkinService
{
    /// <summary>
    /// Creates a walk-in record and inserts a queue entry for the arriving patient.
    ///
    /// AC-1: Persists WalkIn + Appointment (Type=WalkIn, QueueState=Waiting)
    ///       and returns the queue position and estimated wait.
    /// AC-2: When <c>request.ConvertToPatient == true</c>, creates a full User +
    ///       Patient account inline using the provided DateOfBirth and Email.
    /// AC-4: When <c>request.ExistingPatientId</c> is provided, the walk-in is
    ///       linked to the existing patient without duplication.
    /// Edge Case 2: The response includes <c>AtCapacity = true</c> when today's
    ///              queue meets or exceeds the configured capacity threshold.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// ExistingPatientId was provided but no matching patient was found.
    /// </exception>
    Task<WalkinResponse> CreateWalkinAsync(
        CreateWalkinRequest request,
        Guid staffUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Converts an anonymous walk-in to a full registered patient account.
    ///
    /// AC-2: Creates User + Patient records from the provided demographics,
    ///       updates WalkIn.PatientId and the linked Appointment.PatientId,
    ///       and sets WalkIn.IsConverted = true.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No WalkIn exists with <paramref name="walkinId"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The walk-in has already been converted (WalkIn.IsConverted == true).
    /// </exception>
    Task<ConvertWalkinResponse> ConvertWalkinAsync(
        Guid walkinId,
        ConvertWalkinRequest request,
        Guid staffUserId,
        CancellationToken ct = default);
}
