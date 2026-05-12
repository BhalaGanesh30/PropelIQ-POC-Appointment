namespace PropelIQ.Modules.ClinicalIntelligence.Application.Exceptions;

/// <summary>
/// Thrown by <c>CodingDecisionWorkflowService</c> when a clinician attempts to
/// accept, modify, or reject a coding decision after the encounter has already
/// been submitted for billing (US_051, Edge Case 1).
///
/// Maps to HTTP 409 Conflict in <c>CodingDecisionController</c>.
/// The clinician must use the amendment workflow to revise submitted encounters.
/// </summary>
public sealed class EncounterAlreadySubmittedException : Exception
{
    public EncounterAlreadySubmittedException()
        : base("Encounter already submitted; use amendment workflow.")
    {
    }

    public EncounterAlreadySubmittedException(Guid patientId)
        : base($"Encounter for patient {patientId} has already been submitted for billing. Use the amendment workflow to revise coding decisions.")
    {
    }
}
