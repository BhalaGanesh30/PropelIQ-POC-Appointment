using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

/// <summary>
/// Represents a failed OCR job that exhausted all retry attempts.
/// Persisted to the <c>ocr_dead_letter_queue</c> table for manual investigation (AC-4, TR-005).
/// </summary>
public sealed class DeadLetterEntry : BaseEntity
{
    /// <summary>FK to <c>clinical_documents.id</c>.</summary>
    public required Guid DocumentId { get; set; }

    /// <summary>Exception message recorded at the time of final failure.</summary>
    public required string ErrorMessage { get; set; }

    /// <summary>Exception stack trace (nullable — may be absent for non-exception failures).</summary>
    public string? StackTrace { get; set; }

    /// <summary>Total number of attempts made before moving to dead-letter (always ≥ MaxRetries).</summary>
    public int RetryCount { get; set; }
}
