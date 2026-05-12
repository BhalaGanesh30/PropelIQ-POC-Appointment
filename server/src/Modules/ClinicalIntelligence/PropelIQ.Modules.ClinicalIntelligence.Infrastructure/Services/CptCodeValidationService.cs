using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Deterministic CPT code validation service (US_050, FR-MC-002 Hybrid guardrail).
///
/// Filters each LLM-suggested code against the live <c>cpt_codes</c> catalog.
/// Deprecated or non-existent codes are removed before the response is returned (Edge Case 2).
/// </summary>
internal sealed class CptCodeValidationService : ICptCodeValidationService
{
    private readonly ICptCodeRepository _repo;

    public CptCodeValidationService(ICptCodeRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> FilterActiveAsync(
        IEnumerable<string> cptCodes,
        CancellationToken ct = default)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in cptCodes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var isActive = await _repo.ExistsAndActiveAsync(code, ct);
            if (isActive)
            {
                result.Add(code);
            }
        }

        return result;
    }
}
