namespace PropelIQ.Modules.SharedServices.Application.AI;

/// <summary>
/// Thrown when a context chunk's patient scope does not match the requesting patient,
/// indicating a potential cross-patient context leakage attempt (AC-4, AIR-010).
///
/// The exception is thrown by <see cref="IPatientContextAclFilter.ValidateAsync"/>
/// and caught by the AI gateway caller (e.g., <c>CodingAiGatewayClient</c>), which
/// immediately returns a fallback response and logs a forensic audit event.
/// No raw chunk content or PII is included in the exception message.
/// </summary>
public sealed class ACLViolationException : Exception
{
    /// <summary>The patient ID expected by the current request context.</summary>
    public Guid PatientId { get; }

    /// <summary>The patient ID found on the offending context chunk.</summary>
    public Guid ChunkPatientId { get; }

    public ACLViolationException(Guid patientId, Guid chunkPatientId)
        : base($"ACL violation: context chunk patient {chunkPatientId} does not match expected patient {patientId}.")
    {
        PatientId = patientId;
        ChunkPatientId = chunkPatientId;
    }
}
