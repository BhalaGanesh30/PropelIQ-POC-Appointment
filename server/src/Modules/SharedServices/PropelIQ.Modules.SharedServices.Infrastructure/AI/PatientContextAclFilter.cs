using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.AI;

namespace PropelIQ.Modules.SharedServices.Infrastructure.AI;

/// <summary>
/// Defence-in-depth ACL filter for AI context chunks (US_054, AC-4, AIR-010).
///
/// Validates that every <see cref="ContextChunk"/> assembled for the AI prompt belongs
/// to the expected patient before the prompt is dispatched to the LiteLLM gateway.
/// This is a secondary check — the primary ACL is enforced at the pgvector query level
/// via a <c>WHERE patient_id = {patientId}</c> clause in <c>EvidenceRetrievalService</c>.
///
/// On violation, the exception message intentionally omits chunk content to prevent
/// PII from appearing in structured exception logs (NFR-010 forensic log hygiene).
/// </summary>
public sealed class PatientContextAclFilter : IPatientContextAclFilter
{
    private readonly ILogger<PatientContextAclFilter> _logger;

    public PatientContextAclFilter(ILogger<PatientContextAclFilter> logger)
        => _logger = logger;

    /// <inheritdoc />
    public Task ValidateAsync(
        IReadOnlyList<ContextChunk> chunks,
        Guid patientId,
        Guid clinicianId,
        CancellationToken ct = default)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.PatientId != patientId)
            {
                // Log forensic details without chunk content (AC-4, NFR-010).
                _logger.LogError(
                    "ACL violation detected: context chunk {FactId} (type={FactType}) " +
                    "belongs to patient {ChunkPatientId} but request is scoped to patient {PatientId}. " +
                    "Clinician {ClinicianId}. AI call blocked (AIR-010).",
                    chunk.FactId, chunk.FactType, chunk.PatientId, patientId, clinicianId);

                throw new ACLViolationException(patientId, chunk.PatientId);
            }
        }

        _logger.LogDebug(
            "ACL filter passed: {ChunkCount} context chunks validated for patient {PatientId} (clinician {ClinicianId}).",
            chunks.Count, patientId, clinicianId);

        return Task.CompletedTask;
    }
}
