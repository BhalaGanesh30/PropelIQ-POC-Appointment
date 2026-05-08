using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository contract for the <c>ocr_dead_letter_queue</c> table.
/// </summary>
public interface IDeadLetterRepository
{
    /// <summary>Persists a new dead-letter entry for a failed OCR job (AC-4).</summary>
    Task AddAsync(DeadLetterEntry entry, CancellationToken ct = default);

    /// <summary>Returns all dead-letter entries ordered by creation time descending.</summary>
    Task<IReadOnlyList<DeadLetterEntry>> GetAllAsync(CancellationToken ct = default);
}
