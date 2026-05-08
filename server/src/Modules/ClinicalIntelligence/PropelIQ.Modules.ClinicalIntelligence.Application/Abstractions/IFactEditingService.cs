using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Discriminated union result returned by <see cref="IFactEditingService.EditAsync"/>.
/// </summary>
public abstract record EditResult
{
    /// <summary>Edit was applied successfully. Contains the updated fact DTO.</summary>
    public sealed record Success(ClinicalFactResponseDto Dto) : EditResult;

    /// <summary>
    /// Optimistic concurrency conflict — the <c>If-Match</c> ETag did not match the
    /// current <c>row_version</c>. Contains the current (winner) fact for HTTP 409 body.
    /// </summary>
    public sealed record Conflict(ClinicalFactResponseDto CurrentFact) : EditResult;

    /// <summary>The requested fact does not exist. Maps to HTTP 404.</summary>
    public sealed record NotFound : EditResult;
}

/// <summary>
/// Orchestrates authorized clinical fact editing and verification (US_047).
/// Enforces optimistic concurrency, writes immutable audit records, and
/// checks for coding-decision references on each successful edit.
/// </summary>
public interface IFactEditingService
{
    /// <summary>
    /// Edits a clinical fact's name and/or value (AC-1).
    ///
    /// Applies optimistic concurrency via <paramref name="expectedRowVersion"/>:
    /// returns <see cref="EditResult.Conflict"/> when the stored version differs.
    /// On success: updates the fact, writes an audit record, and checks for
    /// coding-decision references (Edge Case 2).
    /// </summary>
    /// <param name="factId">Primary key of the fact to edit.</param>
    /// <param name="request">Patch request containing the new name and/or value.</param>
    /// <param name="expectedRowVersion">ETag parsed from the <c>If-Match</c> request header.</param>
    /// <param name="editorId">Authenticated clinician's user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<EditResult> EditAsync(
        Guid factId,
        PatchFactRequest request,
        int expectedRowVersion,
        Guid editorId,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a fact as verified without changing its content (AC-2).
    ///
    /// Sets <c>verified = true</c>, <c>verified_by</c>, <c>verified_at</c>,
    /// increments <c>row_version</c>, and writes an audit record.
    /// Returns <c>null</c> when the fact does not exist (HTTP 404 caller).
    /// </summary>
    /// <param name="factId">Primary key of the fact to verify.</param>
    /// <param name="verifierId">Authenticated clinician's user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ClinicalFactResponseDto?> VerifyAsync(
        Guid factId,
        Guid verifierId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the edit and verify history for a fact in chronological order (AC-3).
    /// Returns an empty list when no audit records exist.
    /// </summary>
    /// <param name="factId">Primary key of the fact.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<FactAuditEntryDto>> GetHistoryAsync(
        Guid factId,
        CancellationToken ct = default);
}
