namespace PropelIQ.Modules.SharedServices.Application.AI;

/// <summary>
/// Defence-in-depth ACL check for AI context chunks (US_054, AC-4, AIR-010).
///
/// The pgvector HNSW retrieval already enforces a <c>WHERE patient_id = {patientId}</c>
/// clause; this filter is a secondary runtime guard that verifies every <see cref="ContextChunk"/>
/// in the assembled context belongs to the expected patient before the prompt is sent.
///
/// Cross-patient context leakage triggers an <see cref="ACLViolationException"/> which
/// blocks the AI call and logs a forensic audit event (no raw content logged).
/// </summary>
public interface IPatientContextAclFilter
{
    /// <summary>
    /// Validates that every chunk in <paramref name="chunks"/> belongs to <paramref name="patientId"/>.
    /// Throws <see cref="ACLViolationException"/> on the first mismatched chunk (AC-4).
    /// </summary>
    /// <param name="chunks">Context chunks to validate.</param>
    /// <param name="patientId">Expected patient scope for all chunks.</param>
    /// <param name="clinicianId">Requesting clinician (written to violation log).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ACLViolationException">When any chunk belongs to a different patient.</exception>
    Task ValidateAsync(
        IReadOnlyList<ContextChunk> chunks,
        Guid patientId,
        Guid clinicianId,
        CancellationToken ct = default);
}
