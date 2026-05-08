using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.SharedServices.Application.Audit;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Orchestrates clinical fact editing, verification, and history retrieval (US_047).
///
/// Edit flow: fetch → concurrency check → atomic SQL update → audit write → coding decision flag.
/// Verify flow: fetch → set verified fields → atomic SQL update → audit write.
/// History flow: delegate to <see cref="IAuditRepository"/>.
/// </summary>
public sealed class FactEditingService : IFactEditingService
{
    private readonly IClinicalFactRepository    _factRepo;
    private readonly IAuditRepository           _auditRepo;
    private readonly ICodingDecisionRepository  _codingDecisionRepo;
    private readonly IAuditService              _auditService;

    public FactEditingService(
        IClinicalFactRepository   factRepo,
        IAuditRepository          auditRepo,
        ICodingDecisionRepository codingDecisionRepo,
        IAuditService             auditService)
    {
        _factRepo           = factRepo;
        _auditRepo          = auditRepo;
        _codingDecisionRepo = codingDecisionRepo;
        _auditService       = auditService;
    }

    /// <inheritdoc />
    public async Task<EditResult> EditAsync(
        Guid            factId,
        PatchFactRequest request,
        int             expectedRowVersion,
        Guid            editorId,
        CancellationToken ct = default)
    {
        // (a) Fetch current fact — 404 when absent.
        var fact = await _factRepo.GetByIdAsync(factId, ct);
        if (fact is null)
            return new EditResult.NotFound();

        // (b) Capture pre-edit state for audit record.
        var previousName  = fact.Name;
        var previousValue = fact.Value;

        // (c) Apply the requested changes.
        fact.Name       = request.Name  ?? fact.Name;
        fact.Value      = request.Value ?? fact.Value;
        fact.Verified   = true;
        fact.VerifiedBy = editorId;
        fact.VerifiedAt = DateTimeOffset.UtcNow;

        // (d) Atomic update with optimistic concurrency guard.
        var updated = await _factRepo.UpdateAsync(fact, expectedRowVersion, ct);

        if (!updated)
        {
            // Version mismatch — another writer won. Re-fetch the current winner and return 409.
            var current = await _factRepo.GetByIdAsync(factId, ct);
            var currentDto = current is null
                ? new ClinicalFactResponseDto { ETag = string.Empty }
                : MapToResponseDto(current, referencedByCodingDecision: false);
            return new EditResult.Conflict(currentDto);
        }

        // (e) Write immutable audit record (fire-and-forget persistence safety: if this fails
        //     the fact is already updated; healthcare compliance prefers audit over no-op rollback).
        await _auditService.LogEventAsync(
            eventType:        "fact_edited",
            actorUserId:      editorId,
            targetEntityId:   factId,
            targetEntityType: "clinical_fact",
            metadata: new Dictionary<string, string>
            {
                ["previousName"]  = previousName  ?? string.Empty,
                ["previousValue"] = previousValue,
                ["newName"]       = fact.Name     ?? string.Empty,
                ["newValue"]      = fact.Value,
                ["editorId"]      = editorId.ToString(),
            },
            ct: ct);

        // (f) Check whether any coding decision references this fact (Edge Case 2).
        var referencedByCodingDecision = await _codingDecisionRepo.ExistsForFactAsync(factId, ct);

        // (g) Return success with the updated DTO.
        return new EditResult.Success(MapToResponseDto(fact, referencedByCodingDecision));
    }

    /// <inheritdoc />
    public async Task<ClinicalFactResponseDto?> VerifyAsync(
        Guid factId,
        Guid verifierId,
        CancellationToken ct = default)
    {
        var fact = await _factRepo.GetByIdAsync(factId, ct);
        if (fact is null)
            return null;

        var previousVerified  = fact.Verified;
        var currentRowVersion = fact.RowVersion; // Capture before mutation.

        // Apply verification fields without changing name/value.
        fact.Verified   = true;
        fact.VerifiedBy = verifierId;
        fact.VerifiedAt = DateTimeOffset.UtcNow;

        // No If-Match required for verify — use the current row version as the expected version.
        await _factRepo.UpdateAsync(fact, currentRowVersion, ct);

        await _auditService.LogEventAsync(
            eventType:        "fact_verified",
            actorUserId:      verifierId,
            targetEntityId:   factId,
            targetEntityType: "clinical_fact",
            metadata: new Dictionary<string, string>
            {
                ["verifierId"]       = verifierId.ToString(),
                ["previousVerified"] = previousVerified.ToString(),
            },
            ct: ct);

        return MapToResponseDto(fact, referencedByCodingDecision: false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FactAuditEntryDto>> GetHistoryAsync(
        Guid factId,
        CancellationToken ct = default)
    {
        return await _auditRepo.GetByEntityAsync("clinical_fact", factId, ct);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static ClinicalFactResponseDto MapToResponseDto(
        ClinicalFact fact,
        bool         referencedByCodingDecision)
    {
        var doc = fact.Document;
        SourceDocumentDto? sourceDoc = null;

        if (doc is not null)
        {
            sourceDoc = new SourceDocumentDto
            {
                DocumentId  = doc.Id,
                DisplayName = doc.DisplayName ?? doc.FileName,
                UploadedAt  = doc.CreatedAt,
            };
        }

        return new ClinicalFactResponseDto
        {
            FactId                   = fact.Id,
            FactType                 = fact.FactType,
            Name                     = fact.Name,
            Value                    = fact.Value,
            ConfidenceScore          = fact.ConfidenceScore,
            NeedsReview              = fact.NeedsReview,
            Verified                 = fact.Verified,
            FactDate                 = fact.FactDate,
            SourceDocument           = sourceDoc,
            ETag                     = fact.RowVersion.ToString(),
            ReferencedByCodingDecision = referencedByCodingDecision,
        };
    }
}
