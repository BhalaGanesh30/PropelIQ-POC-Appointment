namespace PropelIQ.Modules.Scheduling.Domain.Exceptions;

/// <summary>
/// Thrown by <c>AppointmentStateMachineService</c> when an action is applied
/// to an appointment in a state that does not support that transition (Edge Case 1).
///
/// No database write occurs before this exception is thrown.
/// The API layer maps this to HTTP 422 Unprocessable Entity with a descriptive message.
/// </summary>
public sealed class InvalidStateTransitionException : Exception
{
    /// <summary>
    /// Initialises the exception with a formatted message describing the rejected transition.
    /// </summary>
    /// <param name="action">The action that was attempted.</param>
    /// <param name="currentState">The appointment's current <c>QueueState</c> string value.</param>
    public InvalidStateTransitionException(string action, string currentState)
        : base($"Cannot perform '{action}' on appointment in state '{currentState}'.")
    {
        Action = action;
        CurrentState = currentState;
    }

    /// <summary>The action verb that was rejected.</summary>
    public string Action { get; }

    /// <summary>The state the appointment was in when the rejection occurred.</summary>
    public string CurrentState { get; }
}
