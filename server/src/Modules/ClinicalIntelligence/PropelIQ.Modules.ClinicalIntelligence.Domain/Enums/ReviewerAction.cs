namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;

/// <summary>
/// Lifecycle state of an AI-generated coding suggestion (US_049–US_051).
/// Stored as a VARCHAR(50) string in the database for backward compatibility
/// with the initial schema; mapped via EF Core HasConversion.
///
/// State transitions:
///   Pending → Accepted  : Clinician accepts the AI-suggested code.
///   Pending → Modified  : Clinician changes the code to a different value.
///   Pending → Rejected  : Clinician rejects the suggestion entirely.
/// </summary>
public enum ReviewerAction
{
    /// <summary>AI suggestion awaiting clinician review (initial state).</summary>
    Pending,

    /// <summary>Clinician accepted the AI-suggested ICD-10 code as-is.</summary>
    Accepted,

    /// <summary>Clinician accepted the suggestion but edited the code value.</summary>
    Modified,

    /// <summary>Clinician rejected the suggestion; no code applied.</summary>
    Rejected,
}
