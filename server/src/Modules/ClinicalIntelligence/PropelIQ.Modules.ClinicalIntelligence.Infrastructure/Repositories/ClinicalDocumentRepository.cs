using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Enums;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IClinicalDocumentRepository"/>.
/// Uses the shared <see cref="AppDbContext"/> whose <c>ClinicalDocuments</c> DbSet
/// is already mapped in <c>SharedServices.Infrastructure</c>.
/// </summary>
public sealed class ClinicalDocumentRepository : IClinicalDocumentRepository
{
    private readonly AppDbContext _db;

    public ClinicalDocumentRepository(AppDbContext db) => _db = db;

    public async Task<ClinicalDocument> AddAsync(ClinicalDocument document, CancellationToken ct = default)
    {
        _db.ClinicalDocuments.Add(document);
        await _db.SaveChangesAsync(ct);
        return document;
    }

    public Task<ClinicalDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ClinicalDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<ClinicalDocument>> GetPendingScanDocumentsAsync(CancellationToken ct = default) =>
        await _db.ClinicalDocuments
            .Where(d => d.ScanResult == "PendingScan")
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ClinicalDocument>> GetFailedDocumentsAsync(CancellationToken ct = default) =>
        await _db.ClinicalDocuments
            .Where(d => d.ExtractionStatus == ExtractionStatus.Failed.ToString())
            .ToListAsync(ct);

    public async Task UpdateAsync(ClinicalDocument document, CancellationToken ct = default)
    {
        _db.ClinicalDocuments.Update(document);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateExtractionStatusAsync(Guid documentId, ExtractionStatus status, CancellationToken ct = default)
    {
        await _db.ClinicalDocuments
            .Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.ExtractionStatus, status.ToString()),
                ct);
    }

    public async Task<string?> SearchExtractedTextAsync(Guid documentId, string searchTerm, CancellationToken ct = default)
    {
        // Use FromSqlInterpolated so parameters are passed safely — no SQL injection risk.
        // The GIN index on to_tsvector('english', extracted_text) accelerates the FTS filter.
        // The ILIKE fallback uses pg_trgm trigram similarity for fuzzy matches (typo tolerance).
        var result = await _db.ClinicalDocuments
            .FromSqlInterpolated($"""
                SELECT * FROM app.clinical_documents
                WHERE id = {documentId}
                  AND extracted_text IS NOT NULL
                  AND (
                      to_tsvector('english', extracted_text) @@ plainto_tsquery('english', {searchTerm})
                      OR extracted_text ILIKE '%' || {searchTerm} || '%'
                  )
                """)
            .Select(d => d.ExtractedText)
            .FirstOrDefaultAsync(ct);

        return result;
    }

    /// <inheritdoc />
    public async Task<List<TimelineEventDto>> GetTimelineDocumentsAsync(
        Guid patientId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        // Soft-deleted documents must not appear on the timeline (US_043 AC-3).
        var query = _db.ClinicalDocuments
            .Where(d => d.PatientId == patientId && !d.IsDeleted);

        // Apply date range filters on CreatedAt (UploadedAt equivalent) server-side (AC-3).
        if (from.HasValue)
        {
            query = query.Where(d => d.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(d => d.CreatedAt <= to.Value);
        }

        // Project directly in SQL — avoids loading full entity graph.
        return await query
            .Select(d => new TimelineEventDto
            {
                EventId     = d.Id,
                EventType   = "document_uploaded",
                Category    = "Documents",
                Description = d.DisplayName ?? d.FileName,
                EventDate   = d.CreatedAt,
                PatientId   = d.PatientId,
            })
            .ToListAsync(ct);
    }
}
