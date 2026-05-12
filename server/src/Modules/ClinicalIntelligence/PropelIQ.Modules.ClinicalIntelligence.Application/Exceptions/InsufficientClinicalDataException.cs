namespace PropelIQ.Modules.ClinicalIntelligence.Application.Exceptions;

/// <summary>
/// Thrown by <c>ICodingSuggestionOrchestrator</c> when the patient has no
/// extracted clinical facts to use as coding evidence.
/// Maps to HTTP 422 in <c>CodingSuggestionController</c> (Edge Case 2).
/// </summary>
public sealed class InsufficientClinicalDataException : Exception
{
    public InsufficientClinicalDataException()
        : base("Insufficient clinical data for code suggestion. Please review the patient's clinical profile.")
    {
    }
}
