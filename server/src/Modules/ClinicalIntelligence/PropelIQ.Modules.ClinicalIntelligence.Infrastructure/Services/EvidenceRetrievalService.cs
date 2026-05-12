using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.AiGateway;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// ACL-filtered pgvector HNSW retrieval for coding suggestion evidence (AIR-010).
///
/// Retrieval flow:
///   1. Embed <paramref name="queryText"/> via LiteLLM <c>/embeddings</c> endpoint
///      using <c>text-embedding-3-small</c> (1536 dimensions).
///   2. Execute a patient-scoped HNSW cosine-distance query on
///      <c>clinical_facts.embedding</c> — strictly filtered to <paramref name="patientId"/>
///      to prevent cross-patient context leakage (AIR-010).
///   3. Return the top-<c>topK</c> chunks ordered by cosine distance.
///
/// Falls back to an empty list when:
///   - The embedding API returns null (circuit breaker open).
///   - No facts for the patient have embeddings.
/// </summary>
internal sealed class EvidenceRetrievalService : IEvidenceRetrievalService
{
    private const string EmbeddingModel = "text-embedding-3-small";
    private const int EmbeddingDimensions = 1536;

    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly ILogger<EvidenceRetrievalService> _logger;

    public EvidenceRetrievalService(
        AppDbContext db,
        HttpClient httpClient,
        ILogger<EvidenceRetrievalService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EvidenceChunk>> RetrieveAsync(
        Guid patientId,
        string queryText,
        int topK = 10,
        CancellationToken ct = default)
    {
        // Step 1: Embed the query text.
        var embedding = await GetEmbeddingAsync(queryText, ct);
        if (embedding is null)
        {
            _logger.LogWarning(
                "Embedding call returned null for patient {PatientId}. " +
                "Falling back to empty evidence list.",
                patientId);
            return [];
        }

        // Step 2: pgvector HNSW cosine-distance query with patient-scoped ACL filter (AIR-010).
        // Raw SQL is required since EF Core LINQ does not natively express <=> operator with
        // ORDER BY on a computed expression while also applying the patient_id equality filter.
        var queryVector = new Vector(embedding);

        var results = await _db.ClinicalFacts
            .FromSqlInterpolated($"""
                SELECT *
                FROM   app.clinical_facts
                WHERE  patient_id = {patientId}
                  AND  embedding  IS NOT NULL
                ORDER  BY embedding <=> {queryVector}
                LIMIT  {topK}
                """)
            .AsNoTracking()
            .Select(f => new
            {
                f.Id,
                f.DocumentId,
                f.FactType,
                f.Name,
                f.Value,
                f.FactDate,
            })
            .ToListAsync(ct);

        return results
            .Select((r, i) => new EvidenceChunk(
                FactId:     r.Id,
                DocumentId: r.DocumentId,
                FactType:   r.FactType,
                Name:       r.Name ?? string.Empty,
                Value:      r.Value,
                FactDate:   r.FactDate,
                Distance:   i))   // distance index used as relative rank when raw distance is unavailable
            .ToList();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/embeddings",
                new EmbeddingRequest
                {
                    Model = EmbeddingModel,
                    Input = text,
                },
                ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);

            return result?.Data?.FirstOrDefault()?.Embedding;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to obtain embedding from LiteLLM gateway for model {Model}.",
                EmbeddingModel);
            return null;
        }
    }

    // ── Internal request/response DTOs ────────────────────────────────────────

    private sealed class EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required string Input { get; init; }
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData>? Data { get; init; }
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; init; }
    }
}
