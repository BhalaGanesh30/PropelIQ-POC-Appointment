namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository for querying the <c>cpt_codes</c> reference catalog (US_050, task_003).
/// </summary>
public interface ICptCodeRepository
{
    /// <summary>
    /// Returns the most-recent <c>last_updated_at</c> timestamp across all CPT code rows.
    /// Returns <c>null</c> when the table is empty.
    /// </summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="cptCode"/> exists in the catalog
    /// and its <c>is_deprecated</c> flag is <c>false</c>.
    /// </summary>
    Task<bool> ExistsAndActiveAsync(string cptCode, CancellationToken ct = default);
}
