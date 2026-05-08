namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Computes estimated wait times and overdue status for queue entries (EP-004 US_031 task_003).
/// Implemented by <c>WaitTimeEstimationService</c> in the Infrastructure layer.
///
/// The service is pure — no I/O beyond reading <c>WaitTimeOptions</c> configuration.
/// Register as singleton: the implementation holds only read-only config state.
/// </summary>
public interface IWaitTimeEstimationService
{
    /// <summary>
    /// Returns the estimated wait in minutes for a patient at <paramref name="queuePosition"/>
    /// (0-based) given the appointment type service duration.
    ///
    /// Formula: <c>queuePosition * serviceMinutesForType</c>.
    ///
    /// O(n) contract: callers (<see cref="QueueService"/>) must compute queue positions
    /// via a single LINQ <c>Select((entry, index) =&gt; ...)</c> pass before invoking
    /// this method — O(n) overall, no nested loops.
    /// </summary>
    /// <param name="queuePosition">0-based position in today's ordered queue.</param>
    /// <param name="appointmentTypeCode">
    /// Appointment type string (e.g. "GENERAL", "FOLLOWUP").
    /// Falls back to <c>DefaultServiceDurationMinutes</c> when not in the config dictionary.
    /// </param>
    int CalculateEstimatedWaitMinutes(int queuePosition, string appointmentTypeCode);

    /// <summary>
    /// Returns <see langword="true" /> when the patient has already been waiting
    /// longer than <paramref name="estimatedWaitMinutes"/> since arrival.
    /// Returns <see langword="false" /> when <paramref name="arrivedAt"/> is
    /// <see langword="null" /> (patient not yet checked in — cannot be overdue).
    /// </summary>
    bool IsOverdue(DateTimeOffset? arrivedAt, int estimatedWaitMinutes);
}
